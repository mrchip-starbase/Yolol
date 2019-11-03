﻿using System;
using JetBrains.Annotations;
using Microsoft.Z3;
using Yolol.Analysis.SAT.Extensions;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Type = Yolol.Execution.Type;
using Variable = Yolol.Grammar.AST.Expressions.Variable;

namespace Yolol.Analysis.SAT
{
    public class ModelVariable
        : IModelVariable
    {
        private readonly Model _model;
        private readonly DatatypeExpr _type;
        private readonly IntExpr _numValue;
        private readonly SeqExpr _strValue;
        private readonly BoolExpr _valTaint;

        public ModelVariable(Model model, DatatypeExpr type, IntExpr num, SeqExpr str, BoolExpr taint)
        {
            _model = model;
            _type = type;
            _numValue = num;
            _strValue = str;
            _valTaint = taint;
        }

        internal void AssertEq(Value value)
        {
            var ctx = _model.Context;

            // Bind the type
            AssertEq(value.Type);

            // Since we're binding to a known value this isn't tainted
            _model.Solver.Assert(ctx.MkEq(_valTaint, ctx.MkFalse()));

            switch (value.Type)
            {
                case Type.Number:
                    _model.Solver.Assert(ctx.MkEq(_numValue, _model.Context.MkInt((value.Number.Value * Number.Scale).ToString())));
                    return;

                case Type.String:
                    _model.Solver.Assert(ctx.MkEq(_strValue, _model.Context.MkString(value.String)));
                    return;

                default:
                    throw new InvalidOperationException($"Unknown type `{value.Type}` for value");
            }
        }

        internal void AssertEq(ModelVariable modelVariable)
        {
            _model.Solver.Assert(_model.Context.MkEq(_valTaint, modelVariable._valTaint));
            _model.Solver.Assert(_model.Context.MkEq(_type, modelVariable._type));
            _model.Solver.Assert(_model.Context.MkEq(_numValue, modelVariable._numValue));
            _model.Solver.Assert(_model.Context.MkEq(_strValue, modelVariable._strValue));
        }

        private void AssertEq(Type type)
        {
            var ctx = _model.Context;
            var num = type.HasFlag(Type.Number) ? ctx.MkEq(_type, _model.NumType) : ctx.MkFalse();
            var str = type.HasFlag(Type.String) ? ctx.MkEq(_type, _model.StrType) : ctx.MkFalse();

            _model.Solver.Assert(ctx.MkOr(num, str));
        }

        internal void AssertEq(BaseExpression expr)
        {
            ModelVariable GetOrCreateVar(BaseExpression sub)
            {
                if (sub is Variable var)
                    return _model.GetOrCreateVariable(var.Name);

                if (sub is ConstantNumber num)
                {
                    var n = _model.GetOrCreateVariable(new VariableName(Guid.NewGuid().ToString()));
                    n.AssertEq(num.Value.Value);
                    return n;
                }

                if (sub is ConstantString str)
                {
                    var n = _model.GetOrCreateVariable(new VariableName(Guid.NewGuid().ToString()));
                    n.AssertEq(str.Value);
                    return n;
                }

                throw new NotSupportedException();
            }

            var ctx = _model.Context;
            var sol = _model.Solver;
            var m = _model;

            (BoolExpr nnt, BoolExpr sst, BoolExpr nst, BoolExpr snt) BinaryTypes(BaseBinaryExpression bin, DatatypeExpr nn, DatatypeExpr ss, DatatypeExpr sn, DatatypeExpr ns)
            {
                var l = GetOrCreateVar(bin.Left);
                var r = GetOrCreateVar(bin.Right);

                // set up type checks for the cases
                var nnt = ctx.MkEq(l._type, m.NumType) & ctx.MkEq(r._type, m.NumType);
                var sst = ctx.MkEq(l._type, m.StrType) & ctx.MkEq(r._type, m.StrType);
                var nst = ctx.MkEq(l._type, m.NumType) & ctx.MkEq(r._type, m.StrType);
                var snt = ctx.MkEq(l._type, m.StrType) & ctx.MkEq(r._type, m.NumType);

                // Assert the 4 type cases
                if (nn != null)
                    sol.Assert(ctx.MkImplies(nnt, ctx.MkEq(_type, nn)));
                if (ss != null)
                    sol.Assert(ctx.MkImplies(sst, ctx.MkEq(_type, ss)));
                if (ns != null)
                    sol.Assert(ctx.MkImplies(nst, ctx.MkEq(_type, ns)));
                if (sn != null)
                    sol.Assert(ctx.MkImplies(snt, ctx.MkEq(_type, sn)));

                return (nnt, sst, nst, snt);
            }

            void ComparisonOp(BaseBinaryExpression comparison, Func<IntExpr, IntExpr, BoolExpr> nnr, Func<SeqExpr, SeqExpr, BoolExpr> ssr)
            {
                var (nnt, sst, _, _) = BinaryTypes(comparison, m.NumType, m.NumType, m.NumType, m.NumType);

                var l = GetOrCreateVar(comparison.Left);
                var r = GetOrCreateVar(comparison.Right);

                // Note that this does _not_ propagate taint, in fact it removes it! Although the final result of equality with
                // tainted values cannot be calculated we still know the result is `0 OR 1`.
                // This also does not handle calculating the exact result of mixed equality (string==num, num==string) because
                // that would require converting numbers to strings, which is intractable. Instead it just constrains the value to
                // `0 OR 1`, which is still a useful bound.

                // Comparison are always 0 or 1 (multiplied by 1000 like all numbers)
                sol.Assert(ctx.MkEq(ctx.MkInt(0), _numValue) | ctx.MkEq(ctx.MkInt(1000), _numValue));

                // number<->number comparison (only compute if values are not tainted)
                sol.Assert(ctx.MkImplies(
                    nnt & !l._valTaint & !r._valTaint,
                    ctx.MkEq(_numValue, ctx.MkITE(
                        nnr(l._numValue, r._numValue),
                        ctx.MkInt(1000),
                        ctx.MkInt(0)
                    )
                )));

                // string<->string equality (only compute if values are not tainted)
                sol.Assert(ctx.MkIff(
                    sst & !l._valTaint & !r._valTaint,
                    ctx.MkEq(_numValue, ctx.MkITE(
                        ssr(l._strValue, r._strValue),
                        ctx.MkInt(1000),
                        ctx.MkInt(0)
                    )
                )));
            }

            void LogicalOp(BaseBinaryExpression logical, Func<BoolExpr[], BoolExpr> op)
            {
                var (nnt, sst, nst, snt) = BinaryTypes(logical, m.NumType, m.NumType, m.NumType, m.NumType);

                var l = GetOrCreateVar(logical.Left);
                var r = GetOrCreateVar(logical.Right);

                // logical are always 0 or 1 (multiplied by 1000 like all numbers)
                sol.Assert(ctx.MkEq(ctx.MkInt(0), _numValue) | ctx.MkEq(ctx.MkInt(1000), _numValue));

                // Create the condition for the operator in question
                var opp = op(new[] {
                    ctx.MkNot(ctx.MkEq(l._numValue, ctx.MkInt(0))),
                    ctx.MkNot(ctx.MkEq(r._numValue, ctx.MkInt(0)))
                });

                // if either side is a string the result is `true`, regardless of the other side
                sol.Assert(ctx.MkImplies(sst | nst | snt, ctx.MkEq(_numValue, ctx.MkInt(1000))));
                sol.Assert(ctx.MkImplies(nnt, ctx.MkEq(_numValue, ctx.MkITE(
                    opp,
                    ctx.MkInt(1000),
                    ctx.MkInt(0)
                ))));
            }

            switch (expr)
            {
                case ConstantNumber num:
                    AssertEq(new Value(num.Value));
                    return;

                case ConstantString str:
                    AssertEq(new Value(str.Value));
                    return;

                case Variable var:
                    AssertEq(_model.GetOrCreateVariable(var.Name));
                    return;

                case Add add:
                    {
                        var (nnt, sst, nst, snt) = BinaryTypes(add, m.NumType, m.StrType, m.StrType, m.StrType);

                        var l = GetOrCreateVar(add.Left);
                        var r = GetOrCreateVar(add.Right);

                        // Setup the 2 valid value cases:
                        var nn = ctx.MkAdd(l._numValue, r._numValue);
                        var ss = ctx.MkConcat(l._strValue, r._strValue);

                        // Value is tained if we have one of the intractable type combinations
                        sol.Assert(ctx.MkEq(_valTaint, l._valTaint | r._valTaint | nst | snt));

                        // Assert the values
                        sol.Assert(ctx.MkIff(!_valTaint & nnt, ctx.MkEq(_numValue, nn)));
                        sol.Assert(ctx.MkIff(!_valTaint & sst, ctx.MkEq(_strValue, ss)));

                        return;
                    }

                case Multiply mul:
                    {
                        var (nnt, _, _, _) = BinaryTypes(mul, m.NumType, null, null, null);

                        var l = GetOrCreateVar(mul.Left);
                        var r = GetOrCreateVar(mul.Right);

                        // Value is tained if either side is tainted, or we have an uncomputable type combination
                        sol.Assert(ctx.MkEq(_valTaint, l._valTaint | r._valTaint | !nnt));

                        // Assert number value if type is number
                        sol.Assert(ctx.MkImplies(!_valTaint & nnt, ctx.MkEq(_numValue, (l._numValue * r._numValue) / 1000)));

                        return;
                    }

                case Divide div:
                    {
                        var (nnt, _, _, _) = BinaryTypes(div, m.NumType, null, null, null);

                        var l = GetOrCreateVar(div.Left);
                        var r = GetOrCreateVar(div.Right);

                        // Value is tained if either side is tainted, or we have an uncomputable type combination
                        sol.Assert(ctx.MkEq(_valTaint, l._valTaint | r._valTaint | !nnt));

                        // Assert number value if type is number
                        sol.Assert(ctx.MkImplies(!_valTaint & nnt, ctx.MkEq(_numValue, (l._numValue * 1000) / r._numValue)));

                        return;
                    }

                case EqualTo eq:
                    {
                        ComparisonOp(eq, ctx.MkEq, ctx.MkEq);
                        return;
                    }

                case NotEqualTo neq:
                    {
                        ComparisonOp(neq, (a, b) => ctx.MkNot(ctx.MkEq(a, b)), (a, b) => ctx.MkNot(ctx.MkEq(a, b)));
                        return;
                    }

                case And and:
                    {
                        LogicalOp(and, ctx.MkAnd);
                        return;
                    }

                case Or or:
                    {
                        LogicalOp(or, ctx.MkOr);
                        return;
                    }

                default:
                    // It's not known how to handle this expression, so just taint it
                    sol.Assert(ctx.MkEq(_valTaint, ctx.MkTrue()));

                    throw new NotImplementedException(expr.GetType().Name);
            }
        }

        public bool IsValueAvailable()
        {
            return _model.Solver.IsSatisfiable(_model.Context.MkEq(_valTaint, _model.Context.MkFalse()));
        }

        public bool IsValue(Value v)
        {
            return CanBeValue(v) && CannotBeOtherValue(v);
        }

        public bool CanBeValue(Value v)
        {
            var ctx = _model.Context;

            if (v.Type == Type.String)
            {
                return _model.Solver.IsSatisfiable(
                    ctx.MkAnd(
                        ctx.MkEq(_type, _model.StrType),
                        ctx.MkEq(_strValue, _model.Context.MkString(v.String))
                    )
                );
            }

            if (v.Type == Type.Number)
            {
                return _model.Solver.IsSatisfiable(
                    ctx.MkAnd(
                        ctx.MkEq(_type, _model.NumType),
                        ctx.MkEq(_numValue, _model.Context.MkInt((v.Number.Value * Number.Scale).ToString()))
                    )
                );
            }

            throw new NotSupportedException($"unknown value type {v.Type}");
        }

        public bool CannotBeOtherValue(Value v)
        {
            var ctx = _model.Context;

            // Make a model that asserts it is _not_ the given value, and then check the model is _not_ satisfiable

            if (v.Type == Execution.Type.String)
            {
                return !_model.Solver.IsSatisfiable(ctx.MkNot(ctx.MkEq(_strValue, _model.Context.MkString(v.String))));
            }

            if (v.Type == Execution.Type.Number)
            {
                return !_model.Solver.IsSatisfiable(ctx.MkNot(ctx.MkEq(_numValue, _model.Context.MkInt((v.Number.Value * Number.Scale).ToString()))));
            }

            throw new NotSupportedException($"unknown value type {v.Type}");
        }

        public bool CanBeString()
        {
            return _model.Solver.IsSatisfiable(_model.Context.MkEq(_type, _model.StrType));
        }

        public bool CanBeNumber()
        {
            return _model.Solver.IsSatisfiable(_model.Context.MkEq(_type, _model.NumType));
        }
    }

    public interface IModelVariable
    {
        /// <summary>
        /// Check if the value is computable for this variable
        /// </summary>
        /// <returns></returns>
        bool IsValueAvailable();

        /// <summary>
        /// Check if the model variable can only be the given value
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        bool IsValue(Value v);

        /// <summary>
        /// Check if the model variable can be the given value
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        bool CanBeValue(Value v);

        /// <summary>
        /// Check if the model variable can be a string type
        /// </summary>
        /// <returns></returns>
        bool CanBeString();

        /// <summary>
        /// Check if the model variable can be a number type
        /// </summary>
        /// <returns></returns>
        bool CanBeNumber();
    }
}
