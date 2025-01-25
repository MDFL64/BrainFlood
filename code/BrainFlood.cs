using System.Diagnostics;

public sealed class BrainFlood : Component
{
    const int TIMEOUT = 200;

    public string Source;
    public Output Output;
    public bool UseCompiler;

	protected override void OnStart()
	{
        Exec();
	}

    private async void Exec() {
        await GameTask.WorkerThread();

        try {
            if (UseCompiler) {
                var sw = Stopwatch.StartNew();
                var hell = new DynamicFlood(Source);
                Output.WriteInfo("Compiled type in "+sw.Elapsed);
                
                sw = Stopwatch.StartNew();
                hell.Run(Output);
                Output.WriteInfo("Executed in "+sw.Elapsed);
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
