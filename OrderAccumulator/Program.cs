using OrderAccumulator.Fix;
using OrderAccumulator.Risk;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;

var settings = new SessionSettings(Path.Combine(AppContext.BaseDirectory, "quickfix.cfg"));
var exposureBook = new ExposureBook();
var fixApplication = new OrderFixSessionApplication(exposureBook);
var storeFactory = new FileStoreFactory(settings);
var logFactory = new FileLogFactory(settings);
var acceptor = new ThreadedSocketAcceptor(fixApplication, storeFactory, settings, logFactory);
using var shutdown = new ManualResetEventSlim(false);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Set();
};

acceptor.Start();
Console.WriteLine("OrderAccumulator FIX acceptor started. Press Ctrl+C to stop.");
shutdown.Wait();
acceptor.Stop();
