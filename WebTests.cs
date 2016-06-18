
 # region

using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;

 # endregion

namespace AsyncTaskPatternsPerformanceComparisonInWebApi{
	/*
	PROOF : THAT ASYNC WEB API IS FASTER (>15X) THEN SYNC WEB API
	Using Owin & Web API

	-----------------------------------------------------------------------------------------------------------------------------------------

	using Task.Run for the wrong thing , IE code that is not CPU-bound
	the proper use case of Task.Run: CPU-bound code

	use Task.Run to call CPU-bound methods (from GUI threads). Do not use it just to “provide something awaitable for my async method to use”

	-----------------------------------------------------------------------------------------------------------------------------------------

	http://stackoverflow.com/questions/29168188/asp-net-web-api-2-async-action-methods-with-task-run-performance
	Using await Task.Run to create "async" WebApi is a bad idea
	============================================================
	- you will still use a thread, and even from the same thread pool used for requests.

	It will lead to some unpleasant moments described in good details at this blog:

	Extra (unnecessary) thread switching to the Task.Run thread pool thread. Similarly, when that thread finishes the request, it has to enter the request context (which is not an actual thread switch but does have overhead).
	Extra (unnecessary) garbage is created. Asynchronous programming is a tradeoff: you get increased responsiveness at the expense of higher memory usage. In this case, you end up creating more garbage for the asynchronous operations that is totally unnecessary.
	The ASP.NET thread pool heuristics are thrown off by Task.Run “unexpectedly” borrowing a thread pool thread. I don’t have a lot of experience here, but my gut instinct tells me that the heuristics should recover well if the unexpected task is really short and would not handle it as elegantly if the unexpected task lasts more than two seconds.
	ASP.NET is not able to terminate the request early, i.e., if the client disconnects or the request times out. In the synchronous case, ASP.NET knew the request thread and could abort it. In the asynchronous case, ASP.NET is not aware that the secondary thread pool thread is “for” that request. It is possible to fix this by using cancellation tokens, but that’s outside the scope of this blog post.

	-----------------------------------------------------------------------------------------------------------------------------------------

	The scalability benefits touted for asynchronous implementations
	===========================================
	-are achieved by decreasing the amount of resources you use, and that needs to be baked into the implementation of an asynchronous method… it’s not something achieved by wrapping around it.

	As an example, consider a synchronous method Sleep that doesn’t return for N milliseconds:

	public void Sleep(int millisecondsTimeout){
	Thread.Sleep(millisecondsTimeout);
	}

	Now, consider the need to create an asynchronous version of this, such that the returned Task doesn’t complete for N milliseconds.  Here’s one possible implementation, \
	simply wrapping Sleep with Task.Run to create a SleepAsync:

	=============================

	public Task SleepAsync(int millisecondsTimeout){
	return Task.Run(() => Sleep(millisecondsTimeout));
	}

	and here’s another that doesn’t use Sleep, instead rewriting the implementation to consume fewer resources:

	public Task SleepAsync(int millisecondsTimeout){
	TaskCompletionSource<bool> tcs = null;
	var t = new Timer(delegate { tcs.TrySetResult(true); }, null, –1, -1);
	tcs = new TaskCompletionSource<bool>(t);
	t.Change(millisecondsTimeout, -1);
	return tcs.Task;
	}

	Both of these implementations provide the same basic behavior, both completing the returned task after the timeout has expired.  However, from a scalability perspective, the latter is much more scalable.  The former implementation consumes a thread from the thread pool for the duration of the wait time, whereas the latter simply relies on an efficient timer to signal the Task when the duration has expired.

	 */

	[TestClass]
	public class WebTests{
		private const int TotalNumberOfRequests = 1000;
		private const int TotalServiceProcessingTimeMilliseconds = 10;
		private const int numberOfWorkers = 1;

		[TestMethod]
		public void TestMySync1Web() {
			TestMyWeb("http://localhost:8018/", "sync1");
		}

		[TestMethod]
		public void lamb() {
			TestMyWeb("http://localhost:8128/", "lamb");
		}

		[TestMethod]
		public void TestMySync1ToAsyncWebV2() {
			TestMyWeb("http://localhost:8228/", "Sync1ToAsyncV2");
		}

		[TestMethod]
		public void TestMySync1ToAsyncWebRaw() {
			TestMyWeb("http://localhost:8328/", "sync1toasyncraw");
		}

		[TestMethod]
		public void TestMySync1ToAsyncWeb() {
			TestMyWeb("http://localhost:8428/", "sync1toasync");
		}

		[TestMethod]
		public void TestMySync1ToAsyncWebSync1ToAsyncNoConfigureAwaitFalse() {
			TestMyWeb("http://localhost:8528/", "Sync1ToAsyncNoConfigureAwaitFalse");
		}

		[TestMethod]
		public void TestMySyncWeb() {
			TestMyWeb("http://localhost:8698/", "sync");
		}

		[TestMethod]
		public void TestMyASyncWeb() {
			TestMyWeb("http://localhost:8799/", "async");
		}

		[TestMethod]
		public void TestMyASyncWeb1() {
			TestMyWeb("http://localhost:8079/", "async1");
		}

		[TestMethod]
		public void TestMyASyncWeb2() {
			TestMyWeb("http://localhost:8089/", "async2");
		}

		[TestMethod]
		public void TestMyASyncWeb3() {
			TestMyWeb("http://localhost:8189/", "async3");
		}

		[TestMethod]
		public void TestMyASyncWeb4() {
			TestMyWeb("http://localhost:8289/", "async4");
		}

		[TestMethod]
		public void TestMyASyncWeb5() {
			TestMyWeb("http://localhost:8389/", "async5");
		}

		[TestMethod]
		public void TestActorTell() {
			Helper = new TestHelper();
			var actor = new SlimActor < int,
			int > (numberOfWorkers);

			Helper.Profile(() =  > {
					foreach(var i in Enumerable.Range(0, TotalNumberOfRequests)) {
						var result = actor.Tell(i, (c) =  > Task.FromResult(c * 2), null).Result;
						Assert.AreEqual(true, result);
					}
				});
		}

		[TestMethod]
		public void TestActorTell2() {
			Helper = new TestHelper();
			var actor = new SlimActor < int,
			int > (numberOfWorkers);

			Helper.Profile(() =  > {
					Task.WhenAll(Enumerable.Range(0, TotalNumberOfRequests).AsParallel().Select(async(i) =  > {
								var result = await actor.Tell(i, (c) =  > Task.FromResult(c * 2), null);
								Assert.AreEqual(true, result);
							})).Wait();
				});
		}

		[TestMethod]
		public void TestActorAsk() {
			Helper = new TestHelper();
			var actor = new SlimActor < int,
			int > (numberOfWorkers);
			Helper.Profile(() =  > {
					foreach(var i in Enumerable.Range(0, TotalNumberOfRequests)) {
						var result = actor.Ask(i, (c) =  > Task.FromResult(c * 2), null).Result;
						Assert.AreEqual(i * 2, result);
					}
				});
		}

		[TestMethod]
		public void TestActorAsk2() {
			Helper = new TestHelper();
			var actor = new SlimActor < int,
			int > (numberOfWorkers);
			Helper.Profile(() =  > {
					Task.WhenAll(Enumerable.Range(0, TotalNumberOfRequests).AsParallel().Select(async(i) =  > {
								var result = await actor.Ask(i, (c) =  > Task.FromResult(c * 2), null);
								Assert.AreEqual(i * 2, result);
							})).Wait();
				});
		}

		[TestMethod]
		public void TestActor2() {
			var actor = new SlimActor < int,
			int > (1);
			var actor2 = new SlimActor < string,
			string > (numberOfWorkers);

			foreach(var i in Enumerable.Range(0, TotalNumberOfRequests)) {
				var result = actor.Ask(i, (c) =  > Task.FromResult(c * 2), null).Result;
				Assert.AreEqual(i * 2, result);

				var result2 = actor2.Ask(i.ToString(), (c) =  > Task.FromResult(c + "0"), null).Result;
				Assert.AreEqual(i + "0", result2);
			}
		}

		[TestMethod]
		public void TestEssent1() {
			Helper = new TestHelper();
			PersistentDictionaryFile.DeleteFiles("Names1");
			Assert.IsFalse(PersistentDictionaryFile.Exists("Names1"));
			var dictionary = new PersistentDictionary < string,
			string > ("Names1");
			Helper.Profile(() =  > {
					foreach(var i in Enumerable.Range(1, TotalNumberOfRequests)) {
						//  Console.WriteLine("What is your first name?");
						string firstName = i.ToString();
						var lastName = Guid.NewGuid().ToString();
						if (dictionary.ContainsKey(firstName)) {
							//     Console.WriteLine("Welcome back {0} {1}",firstName,dictionary[firstName]);
						} else {
							// Console.WriteLine("I don't know you, {0}. What is your last name?",firstName);
							dictionary[firstName] = lastName;
						}

						Assert.AreEqual(dictionary[firstName], lastName);
					}
				});
		}

		[TestMethod]
		public void TestEssent2() {
			Helper = new TestHelper();
			PersistentDictionaryFile.DeleteFiles("Names2");
			Assert.IsFalse(PersistentDictionaryFile.Exists("Names2"));
			var dictionary = new PersistentDictionary < string,
			string > ("Names2");
			Helper.Profile(() =  > {
					Task.WhenAll(Enumerable.Range(0, TotalNumberOfRequests).AsParallel().Select(async(i) =  > {
								//  Console.WriteLine("What is your first name?");
								string firstName = i.ToString();
								var lastName = Guid.NewGuid().ToString();
								if (dictionary.ContainsKey(firstName)) {
									//     Console.WriteLine("Welcome back {0} {1}",firstName,dictionary[firstName]);
								} else {
									// Console.WriteLine("I don't know you, {0}. What is your last name?",firstName);
									dictionary[firstName] = lastName;
								}

								Assert.AreEqual(dictionary[firstName], lastName);

								await Task.FromResult(true);
							})).Wait();
				});
		}

		public void TestMyWeb(string endpoint, string action) {
			Helper = new TestHelper(endpoint);

			Helper.StartWebApiServer(endpoint, () =  > {
					//initial request, to warm up the server
					var result = Helper.GetProducts(action).Result;
					Console.WriteLine("Data:" + result.ToList().First());

					Helper.Profile(() =  > {
							var tasks = new List < Task > ();
							for (var i = 0; i < TotalNumberOfRequests; i++) {
								tasks.Add(Helper.GetProducts(action));
								if (tasks.Count % 1000 != 0)
									continue;
								Task.WaitAll(tasks.First());
							}
							Task.WaitAll(tasks.ToArray());
						});
				});
		}

		 # region SetUps
		/*
		packages.config
		<packages>
		<package id="Microsoft.AspNet.WebApi.Client" version="5.2.3" targetFramework="net452" />
		<package id="Microsoft.AspNet.WebApi.Core" version="5.2.3" targetFramework="net452" />
		<package id="Microsoft.AspNet.WebApi.SelfHost" version="5.2.3" targetFramework="net452" />
		<package id="Newtonsoft.Json" version="6.0.4" targetFramework="net452" />
		</packages>
		 */
		private TestHelper Helper {
			set;
			get;
		}

		public class MyApplication{
			public class ProductA{
				public int Id {
					get;
					set;
				}
			}

			public class ProductB{
				public int ProductId {
					get;
					set;
				}
			}

			public class ProductsFactory{
				public static ProductA[]GetProductAs() {
					return new[]{
						new ProductA {
							Id = 1
						}
					};
				}

				public static ProductB[]GetProductBs() {
					return new[]{
						new ProductB {
							ProductId = 1
						}
					};
				}
			}

			public class ProductASlimActor : ASlimActor < ProductAToProductBCommand,
			ProductAToProductBResponse > {
				private IProductConvertionService _productConvertionService {
					set;
					get;
				}

				public ProductASlimActor(int workerCount) : base(workerCount) {
					_productConvertionService = new ProductConvertionService();
				}

				public async Task < ProductAToProductBResponse > Ask(ProductAToProductBCommand command, CancellationToken ? cancelToken) {
					var result = _productConvertionService.ProductAToProductB(command.ProductA);
					return new ProductAToProductBResponse() {
						ProductB = await result
					};
				}
			}

			public class ProductAToProductBCommand{
				public ProductA ProductA {
					set;
					get;
				}
			}

			public class ProductAToProductBResponse{
				public ProductB ProductB {
					set;
					get;
				}
			}

			public interface IProductConvertionService{
				Task < ProductB > ProductAToProductB(ProductA productA);
			}

			public class ProductConvertionService : IProductConvertionService{
				public async Task < ProductB > ProductAToProductB(ProductA productA) {
					return await Task.Factory.StartNew(() =  > new ProductB() {
							ProductId = productA.Id
						});
				}
			}

			public class ProductService{
				public static ProductASlimActor ProductASlimActor = new ProductASlimActor(1);

				public ProductA[]LoadProductLeg() {
					System.Threading.Thread.Sleep(TotalServiceProcessingTimeMilliseconds);
					return ProductsFactory.GetProductAs();
				}

				public async Task < ProductA[] > LoadProductAs() {
					return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());
				}

				public async Task < ProductB > LoadProductB() {
					return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductBs().FirstOrDefault());
				}

				public async Task < ProductB[] > LoadProductBs() {
					var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());

					var result = new List < ProductB > ();
					foreach(var p in products) {
						var tmpResult = await LoadProductB();
						result.Add(tmpResult);
					}

					return result.ToArray();
				}

				public async Task < ProductB[] > LoadProductBs2() {
					var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());
					return await Task.WhenAll(products.AsParallel().Select(async(p) =  > await LoadProductB()));
				}

				public async Task < ProductB[] > LoadProductBs3() {
					var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());
					return await Task.WhenAll(products.Select(async(p) =  > await LoadProductB()));
				}

				public async Task < ProductB[] > LoadProductBs4() {
					var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());
					var productAdvanced = new List < ProductB > ();
					products.AsParallel().ForAll(async(p) =  > {
							productAdvanced.Add(await LoadProductB());
						});
					return productAdvanced.ToArray();
				}

				public async Task < ProductB[] > LoadProductBs5() {
					var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c =  > ProductsFactory.GetProductAs());
					var productAdvanced = new List < ProductB > ();
					var enumerable = products.Select(async(p) =  > {
								var tmpProduct = await ProductASlimActor.Ask(new ProductAToProductBCommand() {
										ProductA = p
									}, null);
								productAdvanced.Add(tmpProduct.ProductB);
							});
					await Task.WhenAll(enumerable);
					return productAdvanced.ToArray();
				}
			}

			public class ProductsController : ApiController{
				public ProductsController() {
					ServicePointManager.MaxServicePointIdleTime = Timeout.Infinite;
				}

				[HttpGet]
				public IEnumerable < ProductA > Sync1() {
					return new ProductService().LoadProductLeg();
				}

				[HttpGet]
				public async Task < HttpResponseMessage > Sync1ToAsync() {
					return await Request.TryPerformOperationAsync(() =  > new ProductService().LoadProductLeg());
				}

				[HttpGet]
				public async Task < HttpResponseMessage > lamb() {
					return await Request.lamb(async() =  > new ProductService().LoadProductLeg());
				}

				[HttpGet]
				public async Task < HttpResponseMessage > Sync1ToAsyncRaw() {
					return await Request.TryPerformOperationAsyncRaw(() =  > new ProductService().LoadProductLeg());
				}

				[HttpGet]
				public async Task < HttpResponseMessage > Sync1ToAsyncV2() {
					return await Request.TryPerformAsyncOperationAsync(() =  > new ProductService().LoadProductAs());
				}

				[HttpGet]
				public async Task < HttpResponseMessage > Sync1ToAsyncNoConfigureAwaitFalse() {
					return await Request.TryPerformOperationAsyncNoConfigureAwaitFalse(() =  > new ProductService().LoadProductLeg());
				}

				[HttpGet]
				public IEnumerable < ProductA > Sync() {
					return new ProductService().LoadProductAs().Result;
				}

				[HttpGet]
				public async Task < IEnumerable < ProductA >> Async() {
					return await new ProductService().LoadProductAs();
				}

				[HttpGet]
				public async Task < ProductB[] > Async1() {
					return await new ProductService().LoadProductBs();
				}

				[HttpGet]
				public async Task < ProductB[] > Async2() {
					return await new ProductService().LoadProductBs2();
				}

				[HttpGet]
				public async Task < ProductB[] > Async3() {
					return await new ProductService().LoadProductBs3();
				}

				[HttpGet]
				public async Task < ProductB[] > Async4() {
					return await new ProductService().LoadProductBs4();
				}

				[HttpGet]
				public async Task < ProductB[] > Async5() {
					return await new ProductService().LoadProductBs5();
				}
			}
		}

		public class TestHelper{
			private readonly string _endpoint;

			public TestHelper(string endpoint = null) {
				// if (string.IsNullOrEmpty(endpoint)) return;

				_endpoint = endpoint;
			}

			private static HttpClient GetClient() {
				var requestHandler = new HttpClientHandler{
					UseCookies = false,
					AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
				};

				return new HttpClient(requestHandler);
			}

			public async Task < IEnumerable < object >> GetProducts(string action) {
				ServicePointManager.MaxServicePointIdleTime = Timeout.Infinite;
				// ThreadPool.SetMinThreads(100, 4);
				var _client = new HttpClient();
				_client.Timeout = TimeSpan.FromMinutes(10);
				_client.BaseAddress = new Uri(_endpoint);

				var result = await _client.GetAsync("api/products/" + action);
				result.EnsureSuccessStatusCode();
				var products = await result.Content.ReadAsAsync < IEnumerable < object >> ().ConfigureAwait(false);

				return products;
			}

			public void Profile(Action operation) {
				var start = DateTime.Now;
				operation();
				var end = DateTime.Now;
				var duration = (end - start).TotalMilliseconds;
				Console.WriteLine("Operation took " + duration + " ms");
				Console.WriteLine("Total Number Of Requests : " + TotalNumberOfRequests);
				Console.WriteLine("Total Service Processing Time Milliseconds : " + TotalServiceProcessingTimeMilliseconds);
			}

			public void StartWebApiServer(string endpoint, Action operation) {
				var config = new HttpSelfHostConfiguration(endpoint);
				config.Routes.MapHttpRoute("API Default", "api/{controller}/{action}/{id}", new {
					id = RouteParameter.Optional
				});
				using(var server = new HttpSelfHostServer(config)) {
					server.OpenAsync().Wait();
					operation();
				}
			}
		}

		 # endregion
	}

	public class SlimActor < T,
	TR >  : ASlimActor < T,
	TR > {
		public SlimActor(int workerCount) : base(workerCount) {}
	}

	public class MailBoxWatcher < TCommand,
	TResponse > {
		public class MailMessage{
			public readonly TCommand Command;
			public readonly TaskCompletionSource < TResponse > TaskSource;
			public readonly Func < TCommand,
			Task < TResponse >> Work;
			public readonly CancellationToken ? CancelToken;

			public MailMessage(
				TCommand command,
				TaskCompletionSource < TResponse > taskSource,
				Func < TCommand, Task < TResponse >> action,
				CancellationToken ? cancelToken) {
				Command = command;
				TaskSource = taskSource;
				Work = action;
				CancelToken = cancelToken;
			}
		}

		public static async Task Watch(BlockingCollection < MailMessage > queue) {
			foreach(var workItem in queue.GetConsumingEnumerable()) {
				if (workItem.CancelToken.HasValue &&
					workItem.CancelToken.Value.IsCancellationRequested) {
					workItem.TaskSource.SetCanceled();
				} else {
					try {
						var result = await workItem.Work(workItem.Command);
						workItem.TaskSource.SetResult(result);
					} catch (OperationCanceledException ex) {
						if (ex.CancellationToken == workItem.CancelToken)
							workItem.TaskSource.SetCanceled();
						else
							workItem.TaskSource.SetException(ex);
					}
					catch (Exception ex) {
						workItem.TaskSource.SetException(ex);
					}
				}
			}
		}
	}

	public abstract class ASlimActor < TCommand,
	TResponse >  : IDisposable{
		private readonly BlockingCollection < MailBoxWatcher < TCommand,
		TResponse > .MailMessage > _mailBox = new BlockingCollection < MailBoxWatcher < TCommand,
		TResponse > .MailMessage > ();

		protected ASlimActor(int workerCount) {
			for (var i = 0; i < workerCount; i++)
				Task.Run(async() =  > await Consume());
		}

		public async Task < bool > Tell(TCommand command, Func < TCommand, Task < TResponse >> work, CancellationToken ? cancelToken) {
			var tcs = new TaskCompletionSource < TResponse > (command);
			_mailBox.Add(new MailBoxWatcher < TCommand, TResponse > .MailMessage(command, tcs, work, cancelToken));
			return await Task.FromResult(true);
		}

		public async Task < TResponse > Ask(TCommand command, Func < TCommand, Task < TResponse >> work, CancellationToken ? cancelToken) {
			var tcs = new TaskCompletionSource < TResponse > (command);
			_mailBox.Add(new MailBoxWatcher < TCommand, TResponse > .MailMessage(command, tcs, work, cancelToken));
			return await tcs.Task;
		}

		private async Task Consume() {
			await MailBoxWatcher < TCommand,
			TResponse > .Watch(_mailBox);
		}

		 # region IDisposable

		private bool _disposed;

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (_disposed)
				return;
			if (disposing) {
				_mailBox.CompleteAdding();
			}
			_disposed = true;
		}

		~ASlimActor() {
			Dispose(false);
		}

		 # endregion
	}

	public static class ApiExtenstions{
		public static async Task < HttpResponseMessage > TryPerformOperationAsync < T > (this HttpRequestMessage request, Func < T > operation) {
			//http://blog.stephencleary.com/2013/11/taskrun-etiquette-examples-dont-use.html
			return await Task.Run(() =  > {
					HttpResponseMessage result;
					try {
						var operationResult = operation();
						result = (request.CreateResponse(HttpStatusCode.OK, operationResult));
					} catch (Exception e) {
						result = (request.CreateResponse(HttpStatusCode.BadRequest, e));
					}
					return result;
				}).ConfigureAwait(false);
		}

		public static async Task < HttpResponseMessage > lamb < T > (this HttpRequestMessage request, Func < Task < T >> operation) {

			HttpResponseMessage result;
			try {
				var operationResult = await operation();
				result = (request.CreateResponse(HttpStatusCode.OK, operationResult));
			} catch (Exception e) {
				result = (request.CreateResponse(HttpStatusCode.BadRequest, e));
			}
			return result;

		}

		public static Task < HttpResponseMessage > TryPerformOperationAsyncRaw < T > (this HttpRequestMessage request, Func < T > function ) {
			//http://blogs.msdn.com/b/pfxteam/archive/2009/06/02/9685804.aspx
			//http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
			//http://blogs.msdn.com/b/pfxteam/archive/2012/03/24/10287244.aspx
			if (function  == null)throw new ArgumentNullException(nameof(function ));
						var tcs = new TaskCompletionSource < HttpResponseMessage > ();
						ThreadPool.QueueUserWorkItem(_ =  > {
								try {
									var operationResult = function ();
									tcs.SetResult(request.CreateResponse(HttpStatusCode.OK, operationResult));
							} catch (Exception exc) {
								tcs.SetResult(request.CreateResponse(HttpStatusCode.BadRequest, exc));
							}
						});
					return tcs.Task;
	}

		public static async Task < HttpResponseMessage > TryPerformAsyncOperationAsync < T > (this HttpRequestMessage request, Func < Task < T >> operation) {
		HttpResponseMessage result;
		try {
			var operationResult = await operation().ConfigureAwait(false);
			result = (request.CreateResponse(HttpStatusCode.OK, operationResult));
		} catch (Exception e) {
			result = (request.CreateResponse(HttpStatusCode.BadRequest, e));
		}
		return result;
	}

		/*
		http://blog.stephencleary.com/2013/11/taskrun-etiquette-examples-dont-use.html

		More to the point, this means that ASP.NET applications should avoid Task.Run
		using Task.Run for asynchronous wrappers is a code smell.
		Conclusion: do not use Task.Run in the implementation of the method; instead, use Task.Run to call the method.
		The request thread is fully capable of running CPU code. If you "push" the work off the request thread by doing
		`await Task.Run(() => ...)`, then you're taking another thread, scheduling the work to run on that thread, and
		then freeing up the request thread - instead of just running the work directly on the request thread.
		So it causes a thread context switch for no benefit (in fact, this technique will run slower and take up more resources for a short time)


		With this async code using Task.Run, instead of a single request thread, this is what happens:

		The request starts processing on an ASP.NET thread.
		Task.Run starts a task on the thread pool to do the calculations. The ASP.NET thread pool has to deal with (unexpectedly) losing one of its threads for the duration of this request.
		The original request thread is returned to the ASP.NET thread pool.
		When the calculation is complete, that thread completes the request and is returned to the ASP.NET thread pool. The ASP.NET thread pool has to deal with (unexpectedly) getting another thread.
		This will work correctly, but it’s not at all efficient.

		There are (at least) four efficiency problems introduced as soon as you use await with Task.Run in ASP.NET:

		Extra (unnecessary) thread switching to the Task.Run thread pool thread. Similarly, when that thread finishes the request, it has to enter the request context (which is not an actual thread switch but does have overhead).
		Extra (unnecessary) garbage is created. Asynchronous programming is a tradeoff: you get increased responsiveness at the expense of higher memory usage. In this case, you end up creating more garbage for the asynchronous operations that is totally unnecessary.
		The ASP.NET thread pool heuristics are thrown off by Task.Run “unexpectedly” borrowing a thread pool thread. I don’t have a lot of experience here, but my gut instinct tells me that the heuristics should recover well if the unexpected task is really short and would not handle it as elegantly if the unexpected task lasts more than two seconds.
		ASP.NET is not able to terminate the request early, i.e., if the client disconnects or the request times out. In the synchronous case, ASP.NET knew the request thread and could abort it. In the asynchronous case, ASP.NET is not aware that the secondary thread pool thread is “for” that request. It is possible to fix this by using cancellation tokens, but that’s outside the scope of this blog post.
		If you have multiple calls to Task.Run, then the performance problems are compounded. On a busy server, this kind of implementation can kill scalability.

		That’s why one of the principles of ASP.NET is to avoid using thread pool threads (except for the request thread that ASP.NET gives you, of course). More to the point, this means that ASP.NET applications should avoid Task.Run.

		Whew! OK, so now we know what the problem is with that implementation. The plain fact is that ASP.NET prefers synchronous methods if the operation is CPU-bound. And this is also true for other scenarios: Console applications, background threads in desktop applications, etc. In fact, the only place we really need an asynchronous calculation is when we call it from the UI thread.

		 */
		public static async Task < HttpResponseMessage > TryPerformOperationAsyncNoConfigureAwaitFalse < T > (this HttpRequestMessage request, Func < T > operation) {
		return await Task.Run(() =  > {
				HttpResponseMessage result;
				try {
					var operationResult = operation();
					result = (request.CreateResponse(HttpStatusCode.OK, operationResult));
				} catch (Exception e) {
					result = (request.CreateResponse(HttpStatusCode.BadRequest, e));
				}
				return result;
			});
	}
		/*



		When we have multiple Task returning asynchronous methods in our hand, we can wait all of them to finish with WaitAll static
		method on Task object. This results several overheads: you will be consuming the asynchronous operations in a blocking fashion and if
		these asynchronous methods is not implemented right, you will end up with deadlocks. At the beginning of this article, we have pointed
		out the usage of ConfigureAwait method. This was for preventing the deadlocks here



		There are 2 reasons for asynchronous code:

		Offloading.
		===========
		Used mostly in GUI threads, or other "more important" threads". (Releasing threads while waiting for CPU operations to complete).
		1.The ability to invoke a synchronous method asynchronously can be very useful for responsiveness, as it allows you to offload
		long-running operations to a different thread.  This isn’t about how many resources you consume, but rather is about which
		resources you consume.  For example, in a UI app, the specific thread handling pumping UI messages is “more valuable” for the
		user experience than are other threads, such as those in the ThreadPool.  So, asynchronously offloading the invocation of a method
		from the UI thread to a ThreadPool thread allows us to use the less valuable resources.  This kind of offloading does not require modification
		to the implementation of the operation being offloaded, such that the responsiveness benefits can be achieved via wrapping.

		2.The ability to invoke a synchronous method asynchronously can also be very useful not just for changing threads, but more generally for escaping the current context

		3.The ability to invoke a synchronous method asynchronously is also important for parallelism.  If, instead, you offload a sub-problem to another thread via asynchronous
		invocation, you can then process the sub-problems concurrently.  As with responsiveness, this kind of offloading does not require modification to the implementation of the
		operation being offloaded, such that parallelism benefits can be achieved via wrapping


		Scalability.
		==============
		Used mainly in the server-side to reduce resource usage. (Releasing threads while waiting for IO to complete).
		The ability to invoke a synchronous method asynchronously does nothing for scalability, because you’re typically still consuming the same amount of resources you would have if you’d invoked it synchronously


		Applications can scale better if operations run asynchronously, but only if there are resources available to service the additional operations.
		================================
		Asynchronous operations ensure that you're never blocking an action because an existing one is in progress. ASP.NET has an asynchronous
		model that allows multiple requests to execute side-by-side. It would be possible to queue the requests up and processes them FIFO,
		but this would not scale well when you have hundreds of requests queued up and each request takes 100ms to process.

		If you have a huge volume of traffic, you may be better off not performing your queries asynchronously, as there may be no additional
		resources to service the requests. If there are no spare resources, your requests are forced to queue up, take exponentially longer or
		outright fail, in which case the asynchronous overhead (mutexes and context-switching operations) isn't giving you anything.
		================================
		Let's first consider a standard synchronous action:

		public ActionResult Index(){
		// some processing
		return View();
		}
		When a request is made to this action a thread is drawn from the thread pool and the body of
		this action is executed on this thread. So if the processing inside this action is slow you are
		blocking this thread for the entire processing, so this thread cannot be reused to process other
		requests. At the end of the request execution, the thread is returned to the thread pool.

		Yes - all threads come from the thread-pool. Your MVC app is already multi-threaded,
		when a request comes in a new thread will be taken from the pool and used to service the request.
		That thread will be 'locked' (from other requests) until the request is fully serviced and completed.
		If there is no thread available in the pool the request will have to wait until one is available.

		If you have async controllers they still get a thread from the pool but while servicing the request
		they can give up the thread, while waiting for something to happen (and that thread can be given to
		another request) and when the original request needs a thread again it gets one from the pool.

		The difference is that if you have a lot of long-running requests (where the thread is waiting for a
		response from something) you might run out of threads from the the pool to service even basic requests.
		If you have async controllers, you don't have any more threads but those threads that are waiting are returned
		to the pool and can service other requests.




		Phillip • 9 days ago
		So is it a bad idea to use Parallel.For/ForEach in ASP.Net? I'm assuming part of the parallel work
		will be done on other threads and the current request's performance increase will come at a cost to the speed of the other requests?

		Stephen Cleary Site Owner  Phillip • 9 days ago
		I never recommend it. It would increase the response time of the first request by decreasing the response time of
		other requests as well as limiting the scalability and responsiveness of the system as a whole.

		Avi • 22 days ago
		"That’s why one of the principles of ASP.NET is to avoid using thread pool threads (except for the request
		thread that ASP.NET gives you, of course). More to the point, this means that ASP.NET applications should avoid Task.Run.
		Whew! OK, so now we know what the problem is with that implementation. The plain fact is that ASP.NET prefers synchronous methods
		if the operation is CPU-bound". What I understand from this is that by using Task.Run() for CPU bound on ASP.Net code we might mess the
		Thread pool heuristics, so it is best to run CPU bound code synchronously. However, what about creating actual threads which and run the
		CPU bound code on those threads. I'm guessing those explicitly created threads are not borrowed from the ASP.Net thread pool

		Stephen Cleary Site Owner  Avi • 22 days ago
		True, but why would you want to? The request already has a thread.

		Avi  Stephen Cleary • 21 days ago
		I was thinking I can keep the request thread active and "running" and run the CPU bound code on explicitly created thread.
		My intention is to keep the request thread from doing CPU bound work and be generally light weight? I think I'm missing something here?

		Stephen Cleary Site Owner  Avi • 21 days ago
		The request thread is fully capable of running CPU code. If you "push" the work off the request thread by doing
		`await Task.Run(() => ...)`, then you're taking another thread, scheduling the work to run on that thread, and then
		freeing up the request thread - instead of just running the work directly on the request thread. So it causes a thread
		context switch for no benefit (in fact, this technique will run slower and take up more resources for a short time).

		Avi  Stephen Cleary • 21 days ago
		Yes, but by pushing the CPU bound work off to another thread, the request thread is free to do other stuff,at
		least that is my understanding. That to me seems beneficial? However by doing the await Task.Run, you explained that we might
		ruin the asp.net threadpool heuristics and therefore better to avoid. I get that. So , my question is instead of running the cpu
		bound code on asp.net request thread and keeping it occupied in the cpu bound code, can I not push the cpu bound operation on to
		another thread(which is not a asp.net thread). So, we get a win-win, we pushed off the computation to another thread(and the request
		thread can do other stuff) and we did not screw up the asp.net heuristics because we didn't push it off to a aps.net threadpool thread.
		In a nutshell, can we push off this cpu bound thread to a non asp.net threadpool thread and free up the request thread?

		Stephen Cleary Site Owner  Avi • 21 days ago
		But you're still using a thread. Freeing up one thread by using another thread is not a benefit.

		What benefit would you get by doing this?

		Avi  Stephen Cleary • 21 days ago
		So, let's say we have one thread initially that is the request thread and it is executing the following pseudo code

		Controller function(){
		DoComputeBoundwork();
		Otherthings();
		}

		If there is only one thread which is the request thread, the processing is synchronous, it will execute the DoComputeBoundwork();and execute there for a while and then go on to execute Otherthings();
		Now, let's say we were able to push off the DoComputeBoundWork to a non asp.net threadpool thread. The main request thread is free to execute Otherthings() while DoComputerBoundwork() is being executed simultaneously in another thread.
		We are able to do work in a parallel fashion instead of sequential. Please correct me if I am wrong or I am missing something(which I think I am).

		Thank you for your patience Stephen.

		Stephen Cleary Site Owner  Avi • 20 days ago
		OK, so you're talking about parallel processing (not asynchronous) on ASP.NET. This is possible but I don't recommend it.
		The reason is that instead of having one thread processing this request, you end up with two. If the second thread is a thread
		pool thread, then you risk throwing off the thread pool heuristics; if the second thread is a manual thread, then you have the
		added cost of creating and destroying a thread per request. In *both* cases, your request ends up using twice as many resources,
		and this can cause scalability problems.
		In theory, parallel work on ASP.NET should be fine as long as you *know* your number of requests will always be low. In reality,
		I've tried it twice and had to take it out both times because it was too easy to bring my server to its knees.

		Avi  Stephen Cleary • 20 days ago
		I see. Now, it makes sense. Thank you for all the knowledge Stephen. Much appreciated.



		 */
	}
}
