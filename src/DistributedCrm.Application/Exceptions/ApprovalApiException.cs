namespace DistributedCrm.Application.Exceptions;

public class ApprovalApiException : Exception
{
    public ApprovalApiException(string message) : base(message)
    {
    }
}