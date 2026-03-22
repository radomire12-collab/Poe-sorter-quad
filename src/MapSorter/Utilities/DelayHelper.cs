namespace MapSorter.Utilities;

public static class DelayHelper
{
    public static void SleepMilliseconds(int delayMs, CancellationToken token)
    {
        if (delayMs <= 0)
        {
            return;
        }

        var remaining = delayMs;
        const int slice = 25;

        while (remaining > 0 && !token.IsCancellationRequested)
        {
            var step = Math.Min(slice, remaining);
            Thread.Sleep(step);
            remaining -= step;
        }
    }
}


