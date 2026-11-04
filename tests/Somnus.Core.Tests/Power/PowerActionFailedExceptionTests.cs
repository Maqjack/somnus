using Somnus.Core.Power;

namespace Somnus.Core.Tests.Power;

public sealed class PowerActionFailedExceptionTests
{
    [Fact]
    public void Constructors_KeepMessageAndCause()
    {
        var cause = new InvalidOperationException("native error 5");

        Assert.False(string.IsNullOrWhiteSpace(new PowerActionFailedException().Message));
        Assert.Equal("Sleep refused.", new PowerActionFailedException("Sleep refused.").Message);

        var wrapped = new PowerActionFailedException("Sleep refused.", cause);
        Assert.Equal("Sleep refused.", wrapped.Message);
        Assert.Same(cause, wrapped.InnerException);
    }
}
