using Unit = System.ValueTuple;
using static ResiliencySample.Resiliency;

Console.WriteLine($"{DateTime.Now} - Start!");
Try("<Unique-Correlation-Id>", () =>
{
    Console.WriteLine("Hello, World!");
    return (Number.Next(20000) > 5000) ? throw new Exception("Something went wrong!") : new Unit();
});

Console.WriteLine($"{DateTime.Now} - Completed!");
Console.ReadLine();