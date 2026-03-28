namespace LayeredChat;

/// <summary>
/// Role of a message in the chat transcript passed to the model connector.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}
