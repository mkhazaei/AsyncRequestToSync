namespace AsyncRequestToSync.Contracts
{
    public interface IMessage
    {
        public Guid CorrelationId { get; }
    }
}
