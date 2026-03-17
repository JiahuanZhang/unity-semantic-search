using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;

namespace SemanticSearch.Editor.Core.Database
{
    public class SemanticSearchDB : IDisposable
    {
        private SqliteConnection _conn;
        private readonly object _lock = new object();

        private const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS assets (
                guid          TEXT PRIMARY KEY,
                asset_path    TEXT NOT NULL,
                md5           TEXT NOT NULL,
                caption       TEXT,
                vector        BLOB,
                vector_dim    INTEGER DEFAULT 0,
                status        TEXT DEFAULT 'Pending',
                updated_at    TEXT NOT NULL
            );";

        private const string UpsertSql = @"
            INSERT OR REPLACE INTO assets
                (guid, asset_path, md5, caption, vector, vector_dim, status, updated_at)
            VALUES
                (@guid, @asset_path, @md5, @caption, @vector, @vector_dim, @status, @updated_at);";

        public static string DefaultDbPath
        {
            get
            {
                var projectRoot = UnityEngine.Application.dataPath;
                // Application.dataPath => "{ProjectRoot}/Assets", 去掉末尾 /Assets
                projectRoot = Path.GetDirectoryName(projectRoot);
                return Path.Combine(projectRoot, "ProjectSettings", "SemanticSearch", "Index.db");
            }
        }

        public void Open(string dbPath = null)
        {
            lock (_lock)
            {
                if (_conn != null) return;

                dbPath = dbPath ?? DefaultDbPath;
                var dir = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _conn = new SqliteConnection($"Data Source={dbPath};Version=3;");
                _conn.Open();

                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL;";
                    cmd.ExecuteNonQuery();
                }

                EnsureSchema();
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                if (_conn == null) return;
                _conn.Close();
                _conn.Dispose();
                _conn = null;
            }
        }

        public void Dispose() => Close();

        private void ThrowIfNotOpen()
        {
            if (_conn == null)
                throw new InvalidOperationException("Database not opened. Call Open() first.");
        }

        public void EnsureSchema()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = CreateTableSql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Upsert(AssetRecord record)
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var tx = _conn.BeginTransaction())
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = UpsertSql;
                    BindRecordParams(cmd, record);
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        public void UpsertBatch(List<AssetRecord> records)
        {
            if (records == null || records.Count == 0) return;

            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var tx = _conn.BeginTransaction())
                {
                    using (var cmd = _conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = UpsertSql;

                        foreach (var record in records)
                        {
                            cmd.Parameters.Clear();
                            BindRecordParams(cmd, record);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
        }

        public AssetRecord GetByGuid(string guid)
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM assets WHERE guid = @guid;";
                    cmd.Parameters.AddWithValue("@guid", guid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        return reader.Read() ? ReadRecord(reader) : null;
                    }
                }
            }
        }

        public AssetRecord GetByPath(string path)
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM assets WHERE asset_path = @path;";
                    cmd.Parameters.AddWithValue("@path", path);
                    using (var reader = cmd.ExecuteReader())
                    {
                        return reader.Read() ? ReadRecord(reader) : null;
                    }
                }
            }
        }

        public List<AssetRecord> GetAllPending()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                var list = new List<AssetRecord>();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM assets WHERE status = 'Pending';";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(ReadRecord(reader));
                    }
                }
                return list;
            }
        }

        public List<(string guid, float[] vector)> GetAllVectors()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                var list = new List<(string, float[])>();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT guid, vector, vector_dim FROM assets WHERE vector IS NOT NULL;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var guid = reader.GetString(0);
                            var blob = (byte[])reader["vector"];
                            var dim = reader.GetInt32(2);
                            var vec = VectorSerializer.Deserialize(blob, dim);
                            if (vec != null)
                                list.Add((guid, vec));
                        }
                    }
                }
                return list;
            }
        }

        public void Delete(string guid)
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var tx = _conn.BeginTransaction())
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM assets WHERE guid = @guid;";
                    cmd.Parameters.AddWithValue("@guid", guid);
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        public void DeleteAll()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var tx = _conn.BeginTransaction())
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM assets;";
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        public Dictionary<string, string> GetAllGuidMd5Map()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                var map = new Dictionary<string, string>();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT guid, md5 FROM assets;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            map[reader.GetString(0)] = reader.GetString(1);
                    }
                }
                return map;
            }
        }

        public int GetCount()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM assets;";
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public int GetPendingCount()
        {
            lock (_lock)
            {
                ThrowIfNotOpen();
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM assets WHERE status = 'Pending';";
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private static void BindRecordParams(SqliteCommand cmd, AssetRecord r)
        {
            cmd.Parameters.AddWithValue("@guid", r.Guid);
            cmd.Parameters.AddWithValue("@asset_path", r.AssetPath);
            cmd.Parameters.AddWithValue("@md5", r.Md5);
            cmd.Parameters.AddWithValue("@caption", (object)r.Caption ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@vector",
                r.Vector != null ? (object)VectorSerializer.Serialize(r.Vector) : DBNull.Value);
            cmd.Parameters.AddWithValue("@vector_dim", r.VectorDim);
            cmd.Parameters.AddWithValue("@status", r.Status.ToString());
            cmd.Parameters.AddWithValue("@updated_at", r.UpdatedAt ?? DateTime.UtcNow.ToString("o"));
        }

        private static AssetRecord ReadRecord(SqliteDataReader reader)
        {
            var record = new AssetRecord
            {
                Guid = reader.GetString(reader.GetOrdinal("guid")),
                AssetPath = reader.GetString(reader.GetOrdinal("asset_path")),
                Md5 = reader.GetString(reader.GetOrdinal("md5")),
                Caption = reader.IsDBNull(reader.GetOrdinal("caption"))
                    ? null : reader.GetString(reader.GetOrdinal("caption")),
                VectorDim = reader.GetInt32(reader.GetOrdinal("vector_dim")),
                UpdatedAt = reader.GetString(reader.GetOrdinal("updated_at"))
            };

            var statusStr = reader.GetString(reader.GetOrdinal("status"));
            record.Status = Enum.TryParse<AssetStatus>(statusStr, out var s) ? s : AssetStatus.Pending;

            if (!reader.IsDBNull(reader.GetOrdinal("vector")))
            {
                var blob = (byte[])reader["vector"];
                record.Vector = record.VectorDim > 0
                    ? VectorSerializer.Deserialize(blob, record.VectorDim)
                    : VectorSerializer.Deserialize(blob);
            }

            return record;
        }
    }
}
