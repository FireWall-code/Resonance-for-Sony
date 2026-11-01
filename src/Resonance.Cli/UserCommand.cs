using Resonance.Core.Device;
using Resonance.Core.Transport;
using static Resonance.Core.Localization.Loc;

/// <summary>Runs an everyday command, via the running app if possible, else on a direct connection.</summary>
static class UserCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.FirstOrDefault() is "help" or "aide" or "-h" or "--help" or "/?")
        {
            Console.WriteLine(CommandRunner.Help);
            return 0;
        }
        if (args.Length == 0) args = ["status"];

        var result = await ControlPipe.TrySendAsync(args, TimeSpan.FromSeconds(45));
        // The app is starting or (re)connecting: wait for it rather than competing for the
        // single control connection the headphones allow.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline
               && (result is { NotReady: true }
                   || result is null && System.Diagnostics.Process.GetProcessesByName("Resonance").Length > 0))
        {
            await Task.Delay(400);
            result = await ControlPipe.TrySendAsync(args, TimeSpan.FromSeconds(45));
        }

        if (result is null)
        {
            Headphones? hp = null;
            // Fast path: the headphones used last time; discovery (several seconds) only if that fails.
            if (DeviceMemory.Last is { } last)
            {
                try { hp = await Headphones.ConnectAsync(last, ReadGroup.None); }
                catch (Exception) { hp = null; }
            }
            ulong? address = null;
            if (hp is null)
            {
                var found = await MdrConnection.FindPairedAsync();
                if (found.Count == 0)
                {
                    Console.Error.WriteLine(T("cli.nopaired"));
                    return 2;
                }
                address = found[0].Address;
            }
            try
            {
                hp ??= await Headphones.ConnectAsync(address!.Value, ReadGroup.None);
                await using (hp) result = await CommandRunner.RunAsync(hp, args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(T("cli.cannotconnect", ex.Message.Split('\n')[0].Trim()));
                return 3;
            }
        }
        (result.Ok ? Console.Out : Console.Error).WriteLine(result.Text);
        return result.Ok ? 0 : 1;
    }
}
