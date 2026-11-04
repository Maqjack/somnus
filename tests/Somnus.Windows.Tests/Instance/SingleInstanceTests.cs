using Somnus.Windows.Instance;

namespace Somnus.Windows.Tests.Instance;

public sealed class SingleInstanceTests
{
    private readonly string _name = "Somnus.Tests." + Guid.NewGuid().ToString("N");

    [Fact]
    public void FirstInstance_IsPrimary_SecondIsNot()
    {
        using var first = new SingleInstance(_name);
        using var second = new SingleInstance(_name);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        Assert.Equal($@"Local\{_name}.Instance", first.MutexName);
    }

    [Fact]
    public void SecondInstance_SignalsTheFirst()
    {
        using var first = new SingleInstance(_name);
        using var activated = new ManualResetEventSlim();
        first.ListenForActivation(activated.Set);

        using (var second = new SingleInstance(_name))
        {
            second.SignalPrimary();
        }

        Assert.True(activated.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), "The first instance was not asked to show its window.");
    }

    [Fact]
    public void AfterTheFirstExits_TheNextLaunchIsPrimaryAgain()
    {
        var first = new SingleInstance(_name);
        first.Dispose();

        using var next = new SingleInstance(_name);

        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void OnlyThePrimaryListens_AndOnlyOnce()
    {
        using var first = new SingleInstance(_name);
        using var second = new SingleInstance(_name);
        first.ListenForActivation(() => { });

        Assert.Throws<InvalidOperationException>(() => first.ListenForActivation(() => { }));
        Assert.Throws<InvalidOperationException>(() => second.ListenForActivation(() => { }));
        Assert.Throws<ArgumentNullException>(() => second.ListenForActivation(null!));
    }

    [Fact]
    public void DisposedInstance_RefusesToSignal()
    {
        var instance = new SingleInstance(_name);
        instance.Dispose();
        instance.Dispose();

        Assert.Throws<ObjectDisposedException>(instance.SignalPrimary);
        Assert.Throws<ObjectDisposedException>(() => instance.ListenForActivation(() => { }));
    }

    [Fact]
    public void Name_IsRequired()
    {
        Assert.Throws<ArgumentException>(() => new SingleInstance(" "));
    }
}
