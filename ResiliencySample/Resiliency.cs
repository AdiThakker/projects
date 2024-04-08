using System.Collections.Concurrent;

namespace ResiliencySample;

enum CircuitState { Closed = 0, HalfOpen = 1, Open = 2 };

public static class Resiliency
{
    public static Random Number = new();

    static ConcurrentDictionary<string, CircuitState> circuitStatus = new();

    public static T Try<T>(string correlationId, Func<T> run, int currentRetry = 0)
    {
        try
        {
            Console.Write($"Attempt: {currentRetry} - ");
            var currentState = circuitStatus.TryGetValue(correlationId, out CircuitState value);
            return value switch
            {
                CircuitState.Closed => run(),
                CircuitState.Open => Break(correlationId, run, "Circuit is Open cannot perform action, Escalating to Circuit-Break!!!"),
                CircuitState.HalfOpen => run(), // go ahead and execute. you could probably use a different strategy here
                _ => throw new NotImplementedException()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            var result = Retry(currentRetry);
            return result switch
            {
                (true, _) => Try(correlationId, run, result.current),
                (false, _) => Break(correlationId, run, "Retries Exhausted, Escalating to Circuit-Break!!!")
            };
        }

        (bool retry, int current) Retry(int currentRetry)
        {
            // Check the exception Type and do retries for specific amount of time
            if (currentRetry >= 3) return (false, -1);

            currentRetry++;
            Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, currentRetry))).GetAwaiter().GetResult();
            return (true, currentRetry);
        }
    }

    static T Break<T>(string correlationId, Func<T> run, string message)
    {
        Console.WriteLine($"{message}");
        try
        {
            circuitStatus.AddOrUpdate(correlationId, CircuitState.Open, (key, state) => state = CircuitState.Open);

            // Set timeout when the timeout elapses close it or change it to half open
            var latestState = Enumerable.Range(1, 3)
                                .Select(count =>
                                {
                                    Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, count))).GetAwaiter().GetResult();                                
                                    var status = Number.Next(int.MaxValue) % 2 == 0; // backup operation status
                                    Console.WriteLine($"Perform backup operation {count}: status succeeded - {status}");
                                    return status;
                                })
                                .Count(_ => _ == false)
                                .GetCurrentState();

            Console.WriteLine($"Timeout complete, Latest State : {latestState}.");
            circuitStatus.AddOrUpdate(correlationId, latestState, (key, state) => state = latestState);

            return Try(correlationId, run);
        }
        catch (Exception)
        {
            // TODO logic to close it 
            throw;
        }
    }

    private static CircuitState GetCurrentState(this int failures )
    {
        return failures switch
        {
            <= 0 => CircuitState.Closed,
            1 or 2 => CircuitState.HalfOpen,
            3 => CircuitState.Open,
            > 3 => throw new Exception("No possible")
        };
    }
}