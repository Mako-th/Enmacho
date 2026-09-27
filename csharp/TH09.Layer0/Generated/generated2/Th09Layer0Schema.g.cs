#nullable enable

namespace TH09.Layer0.Generated;

internal static class Layer0Schema
{
    public const int StatementCount = 4;

    public const int TableCount = 3;

    public const string Fingerprint = "7b2da1d7177b70aa";

    public static readonly string[] Statements =
    [
        "CREATE TABLE session_ticks (\n  session_id INTEGER NOT NULL,\n  segment_no INTEGER NOT NULL,\n  record_version INTEGER NOT NULL,\n  source_kind TEXT NOT NULL,\n  lost_records INTEGER NOT NULL,\n  torn_records INTEGER NOT NULL,\n  tick_count INTEGER NOT NULL,\n  field_order TEXT NOT NULL,\n  encoding TEXT NOT NULL DEFAULT 'colzlib-v1',\n  uncompressed_bytes INTEGER,\n  compressed_bytes INTEGER,\n  blob BLOB NOT NULL,\n  PRIMARY KEY (session_id, segment_no)\n)",
        "CREATE TABLE session_hit_windows (\n  session_id INTEGER NOT NULL,\n  window_no INTEGER NOT NULL,\n  first_seq INTEGER NOT NULL,\n  first_frame INTEGER,\n  tick_count INTEGER NOT NULL,\n  slot_count INTEGER NOT NULL,\n  quant TEXT NOT NULL,\n  hits TEXT NOT NULL,\n  lost_ticks INTEGER NOT NULL DEFAULT 0,\n  field_order BLOB NOT NULL,\n  field_order_encoding TEXT NOT NULL DEFAULT 'json-zlib',\n  encoding TEXT NOT NULL DEFAULT 'colzlib-v1',\n  uncompressed_bytes INTEGER,\n  compressed_bytes INTEGER,\n  blob BLOB NOT NULL,\n  PRIMARY KEY (session_id, window_no)\n)",
        "CREATE TABLE hit_window_features (\n  session_id INTEGER NOT NULL,\n  window_no INTEGER NOT NULL,\n  event_no INTEGER NOT NULL,\n  analysis_version INTEGER NOT NULL,\n  seq INTEGER NOT NULL,\n  side INTEGER NOT NULL,\n  trigger TEXT NOT NULL,\n  json TEXT NOT NULL,\n  PRIMARY KEY (session_id, window_no, event_no)\n)",
        "CREATE INDEX idx_hwf_session ON hit_window_features(session_id)",
    ];

    public static readonly string[] Tables =
    [
        "session_ticks",
        "session_hit_windows",
        "hit_window_features",
    ];

    public static readonly string[] Indexes =
    [
        "idx_hwf_session",
    ];

    public const string SourceKindTick = "tick";

    public const string Encoding = "colzlib-v1";

    public const int DefaultLevel = 6;

    public const int PendingTickLimit = 7200;

    public const int VerifyRecords = 8;

    public const bool VerifyDefault = true;

    public const int LibraryMaxSegmentTicks = 120000;

    public const int CaptureSegmentTicks = 20000;

    public const int BusyTimeoutMs = 5000;

    public const string SynchronousWal = "NORMAL";
}
