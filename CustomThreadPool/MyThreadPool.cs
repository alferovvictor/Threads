using System.Diagnostics;

namespace CustomThreadPool;

public class MyThreadPool : IDisposable
{
	private readonly ThreadPriority _Prioroty;
	private readonly string _Name;
	private readonly Thread[] _Threads;
	private readonly Queue<(Action<object?> Work, object? Parameter)> _WorksQueue = new();
	private readonly CancellationToken _cancellationToken;
	private readonly AutoResetEvent _WorksQueueEvent = new(true);
	private bool CanWork => !_cancellationToken.IsCancellationRequested;
	public string Name => _Name;

	public MyThreadPool(
		int MaxThreadsCount,
		CancellationToken cancellationToken,
		string? Name = null,
		ThreadPriority Prioroty = ThreadPriority.Normal)
	{
		if (MaxThreadsCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(MaxThreadsCount), MaxThreadsCount, "Number of threads count must more than 1 or equal to 1");

		_cancellationToken = cancellationToken;
		_Prioroty = Prioroty;
		_Threads = new Thread[MaxThreadsCount];

		_Name = Name ?? GetHashCode().ToString("x");
		Initialize();
	}

	private void Initialize()
	{
		var thread_pool_name = Name;
		for (var i = 0; i < _Threads.Length; i++)
		{
			var name = $"{nameof(MyThreadPool)}[{thread_pool_name}]-Thread[{i}]";
			var thread = new Thread(WorkingThread)
			{
				Name = name,
				IsBackground = true,
				Priority = _Prioroty
			};
			_Threads[i] = thread;
			thread.Start();
		}
	}

	public void Execute(Action Work) => Execute(null, _ => Work());

	public void Execute(object? Parameter, Action<object?> Work)
	{
		if (!CanWork) throw new InvalidOperationException("Попытка передать задание уничтоженному пулу потоков");

		_WorksQueueEvent.WaitOne(); // запрашиваем доступ к очереди
		if (!CanWork) throw new InvalidOperationException("Попытка передать задание уничтоженному пулу потоков");

		_WorksQueue.Enqueue((Work, Parameter));
		_WorksQueueEvent.Set();    // разрешаем доступ к очереди
	}

	private void WorkingThread()
	{
		var thread_name = Thread.CurrentThread.Name;
		Trace.TraceInformation("Поток {0} запущен с id:{1}", thread_name, Environment.CurrentManagedThreadId);
		var waitTime = TimeSpan.FromMilliseconds(100);

		try
		{
			while (CanWork)
			{
				while (CanWork) // если (до тех пор пока) в очереди нет заданий
				{
					var got = _WorksQueueEvent.WaitOne(waitTime);
					if (got && _WorksQueue.Count > 0)
					{
						// got access - out from cycle
						break;
					}
					else
					{
						_WorksQueueEvent.Set();
					}
				}

				var (work, parameter) = _WorksQueue.Dequeue();
				_WorksQueueEvent.Set(); // разрешаем доступ к очереди

				if (!CanWork) break;

				Trace.TraceInformation("Поток {0}[id:{1}] выполняет задание", thread_name, Environment.CurrentManagedThreadId);
				try
				{
					var timer = Stopwatch.StartNew();
					work(parameter);
					timer.Stop();

					Trace.TraceInformation(
						"Поток {0}[id:{1}] выполнил задание за {2}мс",
						thread_name, Environment.CurrentManagedThreadId, timer.ElapsedMilliseconds);
				}
				catch (ThreadInterruptedException)
				{
					throw;
				}
				catch (Exception e)
				{
					Trace.TraceError("Ошибка выполнения задания в потоке {0}:{1}", thread_name, e);
				}
			}

			Trace.TraceWarning(
			"Поток {0} был принудительно прерван {1}",
			thread_name, Name);
		}
		catch (ThreadInterruptedException)
		{
			Trace.TraceWarning(
				"Поток {0} был принудительно прерван при завершении работы пула {1}",
				thread_name, Name);
		}
		finally
		{
			Trace.TraceInformation("Поток {0} завершил свою работу", thread_name);
		}
	}

	private const int _DisposeThreadJoinTimeout = 100;
	public void Dispose()
	{
		foreach (var thread in _Threads)
			if (!thread.Join(_DisposeThreadJoinTimeout))
				thread.Interrupt();

		_WorksQueueEvent.Dispose();
		Trace.TraceInformation("Пул потоков {0} уничтожен", Name);
	}
}
