﻿using Yolol.Execution;

namespace Yolol.Grammar.AST.Statements
{
    public class EmptyStatement
        : BaseStatement
    {
        public override ExecutionResult Evaluate(MachineState state)
        {
            return new ExecutionResult();
        }

        public override string ToString()
        {
            return "";
        }
    }
}
