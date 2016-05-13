#region

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;

#endregion

namespace AsyncTaskPatternsPerformanceComparisonInWebApi
{
    /*
     PROOF : THAT ASYNC WEB API IS FASTER (>15X) THEN SYNC WEB API
     Using Owin & Web API
     */

    [TestClass]
    public class WebTests
    {
        private const int TotalNumberOfRequests = 1000000;
        private const int TotalServiceProcessingTimeMilliseconds = 10;
        private const int numberOfWorkers = 1;
        [TestMethod]
        public void TestMySyncWeb()
        {
            TestMyWeb("http://localhost:8098/", "sync");
        }

        [TestMethod]
        public void TestMyASyncWeb()
        {
            TestMyWeb("http://localhost:8099/", "async");
        }

        [TestMethod]
        public void TestMyASyncWeb1()
        {
            TestMyWeb("http://localhost:8079/", "async1");
        }

        [TestMethod]
        public void TestMyASyncWeb2()
        {
            TestMyWeb("http://localhost:8089/", "async2");
        }

        [TestMethod]
        public void TestMyASyncWeb3()
        {
            TestMyWeb("http://localhost:8189/", "async3");
        }

        [TestMethod]
        public void TestMyASyncWeb4()
        {
            TestMyWeb("http://localhost:8289/", "async4");
        }

        [TestMethod]
        public void TestMyASyncWeb5()
        {
            TestMyWeb("http://localhost:8389/", "async5");
        }

        public class MyTestActorInt : Actor<int, int>
        {
            public MyTestActorInt(int workerCount) : base(workerCount)
            {
            }
        }

        public class MyTestActorString : Actor<string, string>
        {
            public MyTestActorString(int workerCount) : base(workerCount)
            {
            }
        }




        [TestMethod]
        public void TestActorTell()
        {
            Helper = new TestHelper("http://localhost:8389/");
            var actor = new MyTestActorInt(numberOfWorkers);

            Helper.Profile(() =>
            {
                foreach (var i in Enumerable.Range(0, TotalNumberOfRequests))
                {
                    var result = actor.Tell(i, (c) => Task.FromResult(c * 2), null).Result;
                    Assert.AreEqual(true, result);
                }
            });
        }

        [TestMethod]
        public void TestActorTell2()
        {
            Helper = new TestHelper("http://localhost:8389/");
            var actor = new MyTestActorInt(numberOfWorkers);

            Helper.Profile(() =>
            {
                Task.WhenAll(Enumerable.Range(0, TotalNumberOfRequests).AsParallel().Select(async (i) =>
                {
                    var result = await actor.Tell(i, (c) => Task.FromResult(c * 2), null);
                    Assert.AreEqual(true, result);
                })).Wait();
            });
        }

        [TestMethod]
        public void TestActorAsk()
        {
            Helper = new TestHelper("http://localhost:8389/");
            var actor = new MyTestActorInt(numberOfWorkers);
            Helper.Profile(() =>
            {
                foreach (var i in Enumerable.Range(0, TotalNumberOfRequests))
                {
                    var result = actor.Ask(i, (c) => Task.FromResult(c * 2), null).Result;
                    Assert.AreEqual(i * 2, result);
                }
            });
        }

        [TestMethod]
        public void TestActorAsk2()
        {
            Helper = new TestHelper("http://localhost:8389/");
            var actor = new MyTestActorInt(numberOfWorkers);
            Helper.Profile(() =>
            {
                Task.WhenAll(Enumerable.Range(0, TotalNumberOfRequests).AsParallel().Select(async (i) =>
                {
                    var result = await actor.Ask(i, (c) => Task.FromResult(c * 2), null);
                    Assert.AreEqual(i * 2, result);
                })).Wait();
            });
        }

        [TestMethod]
        public void TestActor2()
        {
            var actor = new MyTestActorInt(1);
            var actor2 = new MyTestActorString(numberOfWorkers);

            foreach (var i in Enumerable.Range(0, TotalNumberOfRequests))
            {
                var result = actor.Ask(i, (c) => Task.FromResult(c * 2), null).Result;
                Assert.AreEqual(i * 2, result);

                var result2 = actor2.Ask(i.ToString(), (c) => Task.FromResult(c + "0"), null).Result;
                Assert.AreEqual(i + "0", result2);
            }
        }

        public void TestMyWeb(string endpoint, string action)
        {
            Helper = new TestHelper(endpoint);

            Helper.StartWebApiServer(endpoint, () =>
            {
                //initial request, to warm up the server
                var result = Helper.GetProducts(action).Result;
                Console.WriteLine("Data:" + result.ToList().First());

                Helper.Profile(() =>
                {
                    var tasks = new List<Task>();
                    for (var i = 0; i < TotalNumberOfRequests; i++)
                    {
                        tasks.Add(Helper.GetProducts(action));
                    }
                    Task.WaitAll(tasks.ToArray());
                });
            });
        }

        #region SetUps
        /*
        packages.config
        <packages>
        <package id="Microsoft.AspNet.WebApi.Client" version="5.2.3" targetFramework="net452" />
        <package id="Microsoft.AspNet.WebApi.Core" version="5.2.3" targetFramework="net452" />
        <package id="Microsoft.AspNet.WebApi.SelfHost" version="5.2.3" targetFramework="net452" />
        <package id="Newtonsoft.Json" version="6.0.4" targetFramework="net452" />
        </packages>
        */
        private TestHelper Helper { set; get; }

        public class MyApplication
        {
            public class ProductA
            {
                public int Id { get; set; }
            }

            public class ProductB
            {
                public int ProductId { get; set; }
            }

            public class ProductsFactory
            {
                public static ProductA[] GetProductAs()
                {
                    return new[]
                    {
                        new ProductA {Id = 1}
                    };
                }

                public static ProductB[] GetProductBs()
                {
                    return new[]
                    {
                        new ProductB {ProductId = 1}
                    };
                }
            }

            public class ProductActor : Actor<ProductAToProductBCommand, ProductAToProductBResponse>
            {
                private IProductConvertionService _productConvertionService { set; get; }

                public ProductActor(int workerCount) : base(workerCount)
                {
                    _productConvertionService = new ProductConvertionService();
                }

                public async Task<ProductAToProductBResponse> Ask(ProductAToProductBCommand command, CancellationToken? cancelToken)
                {
                    var result = _productConvertionService.ProductAToProductB(command.ProductA);
                    return new ProductAToProductBResponse()
                    {
                        ProductB = await result
                    };
                }
            }

            public class ProductAToProductBCommand
            {
                public ProductA ProductA { set; get; }
            }

            public class ProductAToProductBResponse
            {
                public ProductB ProductB { set; get; }
            }

            public interface IProductConvertionService
            {
                Task<ProductB> ProductAToProductB(ProductA productA);
            }

            public class ProductConvertionService : IProductConvertionService
            {
                public async Task<ProductB> ProductAToProductB(ProductA productA)
                {
                    return await Task.Factory.StartNew(() => new ProductB()
                    {
                        ProductId = productA.Id
                    });
                }
            }

            public class ProductService
            {
                public static ProductActor ProductActor = new ProductActor(1);

                public async Task<ProductA[]> LoadProductAs()
                {
                    return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());
                }

                public async Task<ProductB> LoadProductB()
                {
                    return await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductBs().FirstOrDefault());
                }

                public async Task<ProductB[]> LoadProductBs()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());

                    var result = new List<ProductB>();
                    foreach (var p in products)
                    {
                        var tmpResult = await LoadProductB();
                        result.Add(tmpResult);
                    }

                    return result.ToArray();
                }

                public async Task<ProductB[]> LoadProductBs2()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());
                    return await Task.WhenAll(products.AsParallel().Select(async (p) => await LoadProductB()));
                }

                public async Task<ProductB[]> LoadProductBs3()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());
                    return await Task.WhenAll(products.Select(async (p) => await LoadProductB()));
                }

                public async Task<ProductB[]> LoadProductBs4()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());
                    var productAdvanced = new List<ProductB>();
                    products.AsParallel().ForAll(async (p) =>
                    {
                        productAdvanced.Add(await LoadProductB());
                    });
                    return productAdvanced.ToArray();
                }

                public async Task<ProductB[]> LoadProductBs5()
                {
                    var products = await Task.Delay(TimeSpan.FromMilliseconds(TotalServiceProcessingTimeMilliseconds)).ContinueWith(c => ProductsFactory.GetProductAs());
                    var productAdvanced = new List<ProductB>();
                    var enumerable = products.Select(async (p) =>
                    {
                        var tmpProduct = await ProductActor.Ask(new ProductAToProductBCommand() { ProductA = p }, null);
                        productAdvanced.Add(tmpProduct.ProductB);
                    });
                    await Task.WhenAll(enumerable);
                    return productAdvanced.ToArray();
                }
            }

            public class ProductsController : ApiController
            {
                [HttpGet]
                public IEnumerable<ProductA> Sync()
                {
                    return new ProductService().LoadProductAs().Result;
                }

                [HttpGet]
                public async Task<IEnumerable<ProductA>> Async()
                {
                    return await new ProductService().LoadProductAs();
                }

                [HttpGet]
                public async Task<ProductB[]> Async1()
                {
                    return await new ProductService().LoadProductBs();
                }

                [HttpGet]
                public async Task<ProductB[]> Async2()
                {
                    return await new ProductService().LoadProductBs2();
                }

                [HttpGet]
                public async Task<ProductB[]> Async3()
                {
                    return await new ProductService().LoadProductBs3();
                }

                [HttpGet]
                public async Task<ProductB[]> Async4()
                {
                    return await new ProductService().LoadProductBs4();
                }

                [HttpGet]
                public async Task<ProductB[]> Async5()
                {
                    return await new ProductService().LoadProductBs5();
                }
            }
        }

        public class TestHelper
        {
            public TestHelper(string endpoint)
            {
                Client = new HttpClient() { BaseAddress = new Uri(endpoint) };
            }

            private HttpClient Client { set; get; }

            public async Task<IEnumerable<object>> GetProducts(string action)
            {
                var result = await Client.GetAsync("api/products/" + action);
                result.EnsureSuccessStatusCode();
                var products = result.Content.ReadAsAsync<IEnumerable<object>>().Result;
                return products;
            }

            public void Profile(Action operation)
            {
                var start = DateTime.Now;
                operation();
                var end = DateTime.Now;
                var duration = (end - start).TotalMilliseconds;
                Console.WriteLine("Operation took " + duration + " ms");
                Console.WriteLine("Total Number Of Requests : " + TotalNumberOfRequests);
                Console.WriteLine("Total Service Processing Time Milliseconds : " + TotalServiceProcessingTimeMilliseconds);
            }

            public void StartWebApiServer(string endpoint, Action operation)
            {
                var config = new HttpSelfHostConfiguration(endpoint);
                config.Routes.MapHttpRoute("API Default", "api/{controller}/{action}/{id}", new { id = RouteParameter.Optional });
                using (var server = new HttpSelfHostServer(config))
                {
                    server.OpenAsync().Wait();
                    operation();
                }
            }
        }

        #endregion
    }

    public class MailBoxWatcher<TCommand, TResponse>
    {
        public class MailMessage
        {
            public readonly TCommand Command;
            public readonly TaskCompletionSource<TResponse> TaskSource;
            public readonly Func<TCommand, Task<TResponse>> Work;
            public readonly CancellationToken? CancelToken;

            public MailMessage(
                TCommand command,
                TaskCompletionSource<TResponse> taskSource,
                Func<TCommand, Task<TResponse>> action,
                CancellationToken? cancelToken)
            {
                Command = command;
                TaskSource = taskSource;
                Work = action;
                CancelToken = cancelToken;
            }
        }

        public static async Task Watch(BlockingCollection<MailMessage> queue)
        {
            foreach (var workItem in queue.GetConsumingEnumerable())
            {
                if (workItem.CancelToken.HasValue &&
                    workItem.CancelToken.Value.IsCancellationRequested)
                {
                    workItem.TaskSource.SetCanceled();
                }
                else
                {
                    try
                    {
                        var result = await workItem.Work(workItem.Command);
                        workItem.TaskSource.SetResult(result);
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (ex.CancellationToken == workItem.CancelToken)
                            workItem.TaskSource.SetCanceled();
                        else
                            workItem.TaskSource.SetException(ex);
                    }
                    catch (Exception ex)
                    {
                        workItem.TaskSource.SetException(ex);
                    }
                }
            }
        }
    }

    public class Actor<TCommand, TResponse> : IDisposable
    {
        private readonly BlockingCollection<MailBoxWatcher<TCommand, TResponse>.MailMessage> _mailBox = new BlockingCollection<MailBoxWatcher<TCommand, TResponse>.MailMessage>();

        protected Actor(int workerCount)
        {
            for (var i = 0; i < workerCount; i++)
                Task.Run(async () => await Consume());
        }

        public async Task<bool> Tell(TCommand command
            , Func<TCommand, Task<TResponse>> work
            , CancellationToken? cancelToken
            )
        {
            var tcs = new TaskCompletionSource<TResponse>(command);
            _mailBox.Add(new MailBoxWatcher<TCommand, TResponse>.MailMessage(command, tcs, work, cancelToken));
            return await Task.FromResult(true);
        }

        public async Task<TResponse> Ask(TCommand command
          , Func<TCommand, Task<TResponse>> work
          , CancellationToken? cancelToken
          )
        {
            var tcs = new TaskCompletionSource<TResponse>(command);
            _mailBox.Add(new MailBoxWatcher<TCommand, TResponse>.MailMessage(command, tcs, work, cancelToken));
            return await tcs.Task;
        }

        private async Task Consume()
        {
            await MailBoxWatcher<TCommand, TResponse>.Watch(_mailBox);
        }

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _mailBox.CompleteAdding();
            }
            _disposed = true;
        }

        ~Actor()
        {
            Dispose(false);
        }

        #endregion
    }
}