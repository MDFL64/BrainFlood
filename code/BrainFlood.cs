using System.Diagnostics;

public enum Executor {
    Interpreter,
    Compiler,
    LinqExpression
}

public sealed class BrainFlood : Component
{
    const int TIMEOUT = 200;

    public string Source;
    public Output Output;
    public Executor SelectedExecutor;

	protected override void OnStart()
	{
        Exec();
	}

    private async void Exec() {
        await GameTask.WorkerThread();

        try {
            if (SelectedExecutor == Executor.Compiler) {
                var sw = Stopwatch.StartNew();
                var hell = new DynamicFlood(Source);
                Output.WriteInfo("Compiled type in "+sw.Elapsed);
                
                sw = Stopwatch.StartNew();
                hell.Run(Output);
                Output.WriteInfo("Executed in "+sw.Elapsed);
            } else if (SelectedExecutor == Executor.LinqExpression) {
                #if DISABLE_WHITELIST
                var sw = Stopwatch.StartNew();
                var hell = new ExprFlood(Source);
                Output.WriteInfo("Compiled expression in "+sw.Elapsed);
                
                sw = Stopwatch.StartNew();
                hell.Run(Output);
                Output.WriteInfo("Executed in "+sw.Elapsed);
                #else
                throw new Exception("linq expressions blocked by whitelist");
                #endif
            } else {
                FastInterpreter.Run(Source, Output);
            }
        } catch (OperationCanceledException) {
            Log.Info("exec cancelled");
        } catch (Exception e) {
            Log.Info("exception: "+e);
        }
    }

	protected override void OnDestroy()
	{
        if (Output != null) {
            Output.Kill = true;
        }
	}
}
