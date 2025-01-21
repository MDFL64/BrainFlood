using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Diagnostics;

public class Output {
    public string Text = "";
    public string Info = "";
    public bool Kill = false;

    public void WriteInfo(string s) {
        Info += s;
        Info += '\n';
    }

    public void CheckKill() {
        if (Kill) {
            throw new OperationCanceledException();
        }
    }
}

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

class DynamicFlood {
    private Op Root;

    public DynamicFlood(string code) {
        var bytecode = FastInterpreter.Compile(code);
        int index = bytecode.Count-1;

        var op_ty = BuildInner(bytecode,ref index);

        var op = TypeLibrary.Create<Op>(op_ty);
        if (op == null) {
            throw new Exception("failed to create op");
        }

        Root = op;
    }

    public void Run(Output output) {
        int ptr = 1000;
        var data = new byte[1_000_000];
        Root.Run(ptr,data,output);
    }

    private static Type MakeGeneric(Type base_ty, Type[] args) {
        for (int i=0;i<100;i++) {
            var bty = TypeLibrary.GetType(base_ty);
            if (bty == null) {
                Log.Info("retry "+base_ty);
                continue;
            }
            return bty.MakeGenericType(args);
        }
        throw new Exception("bad basetype "+base_ty);
	}

    private static Type GetDigit(int n) {
        switch (n) {
            case 0: return typeof(D0);
            case 1: return typeof(D1);
            case 2: return typeof(D2);
            case 3: return typeof(D3);
            case 4: return typeof(D4);
            case 5: return typeof(D5);
            case 6: return typeof(D6);
            case 7: return typeof(D7);
            case 8: return typeof(D8);
            case 9: return typeof(D9);
            case 0xA: return typeof(DA);
            case 0xB: return typeof(DB);
            case 0xC: return typeof(DC);
            case 0xD: return typeof(DD);
            case 0xE: return typeof(DE);
            case 0xF: return typeof(DF);
        }
        throw new Exception("die");
    }

    private static Type GenerateConst(int n) {
        if (n < 0) {
            return MakeGeneric(typeof(Neg<>),[GenerateConst(-n)]);
        }
        if (n < 16) {
            return GetDigit(n);
        } else if (n < 256) {
            return MakeGeneric(typeof(Num<,>),[GetDigit(n>>4),GetDigit(n&0xF)]);
        } else {
            throw new Exception("const too large "+n);
        }
    }

    private static Type BuildInner(List<Instr> bytecode, ref int index) {
        Type result = typeof(Stop);

        while (index >= 0) {
            var c = bytecode[index];
            switch (c.Op) {
                case OpCode.UpdateCell:
					result = MakeGeneric(typeof(UpdateCell<,,>),[GenerateConst(c.Offset),GenerateConst(c.Inc),result]);
                    break;
                case OpCode.UpdatePointer:
                    result = MakeGeneric(typeof(UpdatePointer<,>),[GenerateConst(c.Offset), result]);
                    break;
                case OpCode.Zero:
                    result = MakeGeneric(typeof(ZeroCell<,>),[GenerateConst(c.Offset), result]);
                    break;
                case OpCode.Output:
                    result = MakeGeneric(typeof(OutputCell<,>),[GenerateConst(c.Offset), result]);
                    break;
                case OpCode.LoopEnd:
                    index--;
                    var body = BuildInner(bytecode,ref index);
                    if (bytecode[index].Op != OpCode.LoopStart) {
                        throw new Exception("BAD LOOP");
                    }
                    result = MakeGeneric(typeof(Loop<,>),[body, result]);
                    break;
                case OpCode.LoopStart:
                    return result;
                default:
                    //Console.WriteLine("--- "+result);
                    throw new Exception("todo "+c.Op);
            }
            index--;
        }

        return result;
    }
}
