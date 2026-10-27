namespace Jinaga.Test.Model;

[FactType("Chat.Channel")]
public record Channel(string name) { }

[FactType("Chat.Message")]
public record Message(Channel channel, string text) { }

[FactType("Chat.Read")]
public record Read(Message message, User user) { }
