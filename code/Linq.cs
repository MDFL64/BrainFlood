using System.IO;
using System.Linq.Expressions;

class ExprVars {
    public ParameterExpression Index;
    public ParameterExpression Data;
    public ParameterExpression Output;
}

public class ExprFlood {
    private Action<int,byte[],Output> Root;

    public ExprFlood(string code) {
        var bytecode = FastInterpreter.Compile(code);
        int index = bytecode.Count-1;
        var vars = new ExprVars{
            Index = Expression.Parameter(typeof(int)),
            Data = Expression.Parameter(typeof(byte[])),
            Output = Expression.Parameter(typeof(Output))
        };

        var root_expr = BuildInner(bytecode,ref index, vars);

        Root = Expression.Lambda<Action<int,byte[],Output>>(root_expr, [vars.Index, vars.Data, vars.Output]).Compile();

        //Root = op;
    }

    public void Run(Output output) {
        int ptr = 1000;
        var data = new byte[1_000_000];
        Root(ptr,data,output);
    }

    private static Expression GenerateConst(int n) {
        return Expression.Constant(n);
    }

    private static Expression BuildInner(List<Instr> bytecode, ref int index, ExprVars vars) {
        List<Expression> children = new List<Expression>();

        while (index >= 0) {
            var c = bytecode[index];
            switch (c.Op) {
                case OpCode.UpdateCell: {
                    var offset_index = Expression.Add(vars.Index, GenerateConst(c.Offset));
                    var cell = Expression.ArrayAccess(vars.Data, offset_index);
                    var new_val = Expression.Add( Expression.Convert(cell, typeof(int)), GenerateConst(c.Inc) );
                    children.Add( Expression.Assign(cell, Expression.Convert(new_val, typeof(byte)) ) );
                    break;
                }
                case OpCode.UpdatePointer: {
                    children.Add( Expression.AddAssign(vars.Index, GenerateConst(c.Offset)) );
                    break;
                }
                case OpCode.Zero: {
                    var offset_index = Expression.Add(vars.Index, GenerateConst(c.Offset));
                    var cell = Expression.ArrayAccess(vars.Data, offset_index);
                    children.Add( Expression.Assign(cell, Expression.Constant((byte)0) ) );
                    break;
                }
                case OpCode.Output: {
                    var offset_index = Expression.Add(vars.Index, GenerateConst(c.Offset));
                    var cell = Expression.ArrayAccess(vars.Data, offset_index);
                    var cell_char = Expression.Convert(cell,typeof(char));
                    children.Add( Expression.Call(vars.Output,typeof(Output).GetMethod("WriteChar"),cell_char) );
                    break;
                }
                case OpCode.LoopEnd: {
                    index--;
                    var body = BuildInner(bytecode,ref index, vars);
                    if (bytecode[index].Op != OpCode.LoopStart) {
                        throw new Exception("BAD LOOP");
                    }

                    var check_kill = Expression.Call(vars.Output,typeof(Output).GetMethod("CheckKill"));

                    var cell = Expression.ArrayIndex(vars.Data, vars.Index);
                    var test = Expression.NotEqual(cell, Expression.Constant((byte)0));
                    var exit = Expression.Label();

                    var loop = Expression.Loop(Expression.IfThenElse(
                        test,
                        Expression.Block(check_kill,body),
                        Expression.Break(exit)
                    ),exit);
                    children.Add( loop );
                    break;
                }
                case OpCode.LoopStart:
                    goto finish;
                default:
                    //Console.WriteLine("--- "+result);
                    throw new Exception("todo "+c.Op);
            }
            index--;
        }
        finish:

        children.Reverse();

        return Expression.Block(children);
    }
}
