using System.Collections.Concurrent;
using Unit = System.ValueTuple;

Random number = new();
ConcurrentDictionary<string, CircuitState> circuitStatus = new();

Console.WriteLine($"{DateTime.Now} - Start!");
Try<Unit>("<Unique-Correlation-Id>", () =>
{
    Console.WriteLine("Hello, World!");
    return (number.Next(20000) > 5000) ? throw new Exception("Something went wrong!") : new Unit();
});

Console.WriteLine($"{DateTime.Now} - Completed!");
Console.ReadLine();

T Try<T>(string correlationId, Func<T> run, int currentRetry = 0)
{
    try
    {
        Console.Write($"Current Attempt: {currentRetry} - ");
        var currentState = circuitStatus.TryGetValue(correlationId, out CircuitState value);
        return value switch
        {
            CircuitState.Closed => run(),
            CircuitState.Open => throw new InvalidOperationException("Circuit is Open cannot perform action"),
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
            (false, _) => Break(correlationId, run)
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

T Break<T>(string correlationId, Func<T> run)
{
    Console.WriteLine($"{correlationId} - Retries Exhausted, Escalating to Circuit-Break!!!");
    try
    {
        circuitStatus.AddOrUpdate(correlationId, CircuitState.Open, (key, state) => state = CircuitState.Open);

        // Set timeout when the timeout elapses close it or change it to half open
        var failures = Enumerable.Range(1, 3)
                            .Select(count =>
                            {
                                Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, count))).GetAwaiter().GetResult();                                
                                var status = (number.Next(int.MaxValue) % 2 == 0 ? true : false); // backup operation status
                                Console.WriteLine($"Perform backup operation {count}: status succeeded - {status}");
                                return status;
                            })
                            .Count(_ => false);
        
        var latestState = failures switch
        {
            <= 0 => CircuitState.Closed,
            1 or 2 => CircuitState.HalfOpen,
            3 => CircuitState.Open,
            > 3 => throw new Exception("No possible")
        };
        

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

enum CircuitState { Closed = 0, HalfOpen = 1, Open = 2 };