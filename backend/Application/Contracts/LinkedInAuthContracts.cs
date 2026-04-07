namespace backend.Application.Contracts;

public sealed record LinkedInLoginRequest(string Username, string Password);

public sealed record LinkedInLoginResponse(string Message, bool IsSessionValid);