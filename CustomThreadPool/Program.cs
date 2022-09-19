using CustomThreadPool;

var messages = Enumerable.Range(1, 1000).Select(i => $"Message-{i}");

var tokenSource = new CancellationTokenSource();

using var thread_pool = new MyThreadPool(
	MaxThreadsCount: 10,
	cancellationToken: tokenSource.Token,
	Name: "My Thread Pool");

Task.Run(async () =>
{
	await Task.Delay(2000);
	tokenSource.Cancel();
});

foreach (var message in messages)
{
	thread_pool.Execute(message, obj =>
	{
		var msg = (string)obj!;
		Console.WriteLine(">> Обработка сообщения {0} начата...", msg);
		Thread.Sleep(100);
		Console.WriteLine(">> Обработка сообщения {0} выполнена", msg);
	});
}

Console.WriteLine("Press ENTER to EXIT");

Console.ReadLine();
