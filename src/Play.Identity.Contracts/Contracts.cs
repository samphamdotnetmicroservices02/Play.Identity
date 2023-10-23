namespace Play.Identity.Contracts;

public record DebitGil(Guid UserId, decimal Gil, Guid CorrelationId);

public record GilDebited(Guid CorrelationId);

//we use the wrod NewTotalGil just to make sure that it is clear, that's is the complete total amount of gil that
// this user has now, regardless of how much was debited or credited in the previous operation
public record UserUpdated(Guid UserId, string Email, decimal NewTotalGil);