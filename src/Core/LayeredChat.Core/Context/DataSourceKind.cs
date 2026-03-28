namespace LayeredChat;

/// <summary>
/// Declares how a data source is intended to be used for documentation and host wiring—not a security boundary.
/// </summary>
public enum DataSourceKind
{
    Unspecified,
    VectorSemantic,
    SqlTabular,
    KeyValue,
    FileOrBlob,
    Http,
    Custom
}
