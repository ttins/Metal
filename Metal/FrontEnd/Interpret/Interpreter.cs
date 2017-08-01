﻿using Metal.Diagnostics.Runtime;
using Metal.FrontEnd.Grammar;
using Metal.FrontEnd.Analyze;
using Metal.FrontEnd.Scan;
using System;
using System.Collections.Generic;
using Metal.Intermediate;
using System.Collections;

namespace Metal.FrontEnd.Interpret {
  public class Interpreter : Expression.IVisitor<object>, Statement.IVisitor<object> {


    MetalEnvironment globals = new MetalEnvironment();
    MetalEnvironment environment;

    public Interpreter() {
      globals.Define("clock", new MetalType.Callable ((arg1, arg2) => {
        long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        return milliseconds;
      }));
      environment = globals;
    }

    public void ResetEnvironment() {
      environment = new MetalEnvironment();
    }


    /* Visit Statements */
    public object Visit(Statement.Expr statement) {
      Evaluate(statement.Expression);
      return null;
    }
    public object Visit(Statement.Print statement) {
      Object value = Evaluate(statement.Expression);
      Console.WriteLine(value ?? "null");
      return null;
    }

    public object Visit(Statement.Block statement) {
      ExecuteBlock(statement.Statements, new MetalEnvironment(environment));
      return null;
    }

    public object Visit(Statement.Var statement) {

      object value = null;
      if (statement.Initializer != null) {
        value = Evaluate(statement.Initializer);
      }
      environment.Define(statement.Name.Lexeme, value);
      return null;
    }

    public object Visit(Statement.Function statement) {
      MetalType.Function function = new MetalType.Function(statement);
      environment.Define(statement.Name.Lexeme, function);
      return null;
    }

    public object Visit(Statement.If statement) {
      if (IsTruthy(statement.Condition)) {
        Execute(statement.ThenBranch);
      } else if (statement.ElseBranch != null) {
        Execute(statement.ElseBranch);
      }
      return null;
    }

    public object Visit(Statement.While statement) {
      while (IsTruthy(Evaluate(statement.Condition))) {
        Execute(statement.Body);
      }
      return null;
    }

    public object Visit(Statement.RepeatWhile statement) {
      do {
        Execute(statement.Body);
      } while (IsTruthy(Evaluate(statement.Condition)));
      return null;
    }

    public object Visit(Statement.For statement) {
      environment.Define(statement.Name.Lexeme, null);
      foreach (var value in (IEnumerable)Evaluate(statement.Range)) {
        environment.Assign(statement.Name, value);
        Execute(statement.Body);
      }
      return null;
    }

    /* Visit Expressions */

    public object Visit(Expression.Assign expression) {
      object value = Evaluate(expression.Value);
      environment.Assign(expression.Name, value);
      return value;
    }

    public object Visit(Expression.Logical expression) {
      object left = Evaluate(expression.Left);
      if (expression.Operator.IsOperator("or")) {
        if (IsTruthy(left)) return left;
      } else {
        if (!IsTruthy(left)) return left;
      }
      return Evaluate(expression.Right);
    }

    public object Visit(Expression.Literal expression) {
      return expression.Value;
    }

    public object Visit(Expression.Parenthesized expression) {
      return Evaluate(expression.Center);
    }

    public object Visit(Expression.Unary expression) {
      object right = Evaluate(expression.Right);
      if (expression.Operator.IsOperator("-")) {
        CheckNumberOperand(expression.Operator, right);
        if (right is double) return -(double)right;
        else return -(int)right;
      }
      if (expression.Operator.IsOperator("!")) {
        return !IsTruthy(right);
      }
      return null;
    }

    public object Visit(Expression.Binary expression) {
      object left = Evaluate(expression.Left);
      object right = Evaluate(expression.Right);
      // '+' operator
      if (expression.Operator.IsOperator("+")) {

        CheckNullOperand(expression.Operator, left, right);

        if (left is string || right is string) {
          //Console.WriteLine(string.Format("left {0}, right {1}", left.ToString(), right.ToString()));
          return left.ToString() + right.ToString();
        }

        if (left is int && right is int) {
          return ApplyIntOperatorToOperand(ConvertObjectToInt(left, right), "+");
        }

        if ((left is int || left is double) && (right is int || right is double)) {
          return ApplyDoubleOperatorToOperand(ConvertObjectToDouble(left, right), "+");
        }
      }

      // '-' operator
      if (expression.Operator.IsOperator("-")) {
        if (left is int && right is int) {
          return ApplyIntOperatorToOperand(ConvertObjectToInt(left, right), "-");
        }
        return ApplyDoubleOperatorToOperand(ConvertObjectToInt(left, right), "-");
      }

      // '*' operator
      if (expression.Operator.IsOperator("*")) {

        if (left is int && right is int) {
          return ApplyIntOperatorToOperand(ConvertObjectToInt(left, right), "*");
        }

        if ((left is int || left is double) && (right is int || right is double)) {
          return ApplyDoubleOperatorToOperand(ConvertObjectToInt(left, right), "*");
        }
      }

      // '/' operator
      if (expression.Operator.IsOperator("/")) {
        if (left is int && right is int) {
          return ApplyIntOperatorToOperand(ConvertObjectToInt(left, right), "/");
        }
        if ((left is int || left is double) && (right is int || right is double)) {
          return ApplyDoubleOperatorToOperand(ConvertObjectToDouble(left, right), "/");
        }
      }

      // '>', '>=', '<', '<=' operators
      if (expression.Operator.IsOperator(">")) {
        CheckNumberOperand(expression.Operator, left, right);
        return ApplyBoolOperatorToOperand(ConvertObjectToDouble(left, right), ">");
      }
      if (expression.Operator.IsOperator(">=")) {
        CheckNumberOperand(expression.Operator, left, right);
        return ApplyBoolOperatorToOperand(ConvertObjectToDouble(left, right), ">=");
      }
      if (expression.Operator.IsOperator("<")) {
        CheckNumberOperand(expression.Operator, left, right);
        return ApplyBoolOperatorToOperand(ConvertObjectToDouble(left, right), "<");
      }
      if (expression.Operator.IsOperator("<=")) {
        CheckNumberOperand(expression.Operator, left, right);
        return ApplyBoolOperatorToOperand(ConvertObjectToDouble(left, right), "<=");
      }

      // Check equality
      if (expression.Operator.IsOperator("!=")) return !IsEqual(left, right);
      if (expression.Operator.IsOperator("==")) return IsEqual(left, right);

      if (expression.Operator.IsOperator("..")) {
        CheckNumberOperand(expression.Operator, left, right);
        return ApplyRangeOperator(ConvertObjectToInt(left, right));
      }

      return null;
    }

    public object Visit(Expression.Call expression) {
      object callee = Evaluate(expression.Callee);
      List<object> arguments = new List<object>();
      foreach (var argument in expression.Arguments) {
        arguments.Add(Evaluate(argument));
      }
      if (!(callee is MetalType.ICallable)) {
        throw new RuntimeError(expression.Parenthesis,
        "Can only call functions and classes");
      }
      MetalType.ICallable function = (MetalType.ICallable)callee;

      if (arguments.Count != function.Arity) {
        throw new RuntimeError(expression.Parenthesis,
          string.Format("Expected {0} arguments but got {1}.", function.Arity, arguments.Count));
      }

      return function.Call(this, arguments);
    }

    public object Visit(Expression.Variable expression) {
      return environment.Get(expression.Name);
    }

    /* Helpers */

    private (int, int) ConvertObjectToInt(object left, object right) {
      int.TryParse(left.ToString(), out int a);
      int.TryParse(right.ToString(), out int b);
      //Console.WriteLine(string.Format("a {0}, b {1}", a, b));
      return (a, b);
    }

    private (double, double) ConvertObjectToDouble(object left, object right) {
      double.TryParse(left.ToString(), out double a);
      double.TryParse(right.ToString(), out double b);
      //Console.WriteLine(string.Format("a {0}, b {1}", a, b));
      return (a, b);
    }

    private int ApplyIntOperatorToOperand((int, int) operands, string operand) {
      int a = operands.Item1, b = operands.Item2;
      switch (operand) {
        case "+": return a + b;
        case "-": return a - b;
        case "*": return a * b;
        case "/": {
            if (b == 0) throw new RuntimeError("Division by zero.");
            return a / b;
          }
      }
      return 0;
    }

    private double ApplyDoubleOperatorToOperand((double, double) operands, string operand) {
      double a = operands.Item1, b = operands.Item2;
      switch (operand) {
        case "+": return a + b;
        case "-": return a - b;
        case "*": return a * b;
        case "/": {
            if (Equals(b, 0)) Metal.RuntimeError(new RuntimeError("Division by zero."));
            return a / b;
          }
      }
      return 0.0f;
    }

    private bool ApplyBoolOperatorToOperand((double, double) operands, string operand) {
      switch (operand) {
        case ">": return operands.Item1 > operands.Item2;
        case ">=": return operands.Item1 >= operands.Item2;
        case "<": return operands.Item1 < operands.Item2;
        case "<=": return operands.Item1 <= operands.Item2;
      }
      return false;
    }

    private object ApplyRangeOperator((int, int) operands) {
      var a = operands.Item1;
      var b = operands.Item2;
      return CreateRange(a, b);
    }

    IEnumerable<int> CreateRange(int a, int b) {
      var increment = b > a ? 1 : -1;
      for (var i = a; i != b; i += increment)
        yield return i;
      yield return b;
    }

    private void CheckNullOperand(Token @operator, object left, object right) {
      if (left == null || right == null) {
        throw new RuntimeError(@operator, "Operand must not be null.");
      }
    }

    private void CheckNumberOperand(Token @operator, object operand) {
      if (operand is int || operand is double) return;
      throw new RuntimeError(@operator, "Operand must be a number.");
    }

    private void CheckNumberOperand(Token @operator, object left, object right) {
      if ((left is int || left is double) && (right is int || right is double)) return;
      throw new RuntimeError(@operator, "Operands must be numbers.");
    }

    private bool IsTruthy(object @object) {
      if (@object == null) return false;
      if (@object is Boolean) return (bool)@object;
      return true;
    }

    private bool IsEqual(object a, object b) {
      if (a == null && b == null) return true;
      if (a == null) return false;
      return a.Equals(b);
    }

    private object Evaluate(Expression expression) {
      return expression.Accept(this);
    }

    internal string Interpret(Expression expression) {
      try {
        object value = Evaluate(expression);
        return value?.ToString();
      } catch (RuntimeError error) {
        Metal.RuntimeError(error);
        return null;
      }
    }

    internal void Interpret(List<Statement> statements) {
      try {
        foreach (var statement in statements) {
          Execute(statement);
        }
      } catch (RuntimeError error) {
        Metal.RuntimeError(error);
      }
    }

    public void Execute(Statement statement) {
      if (statement != null) {
        statement.Accept(this);
      }
    }

    public void ExecuteBlock(List<Statement> statements, MetalEnvironment environment) {
      if (environment == null)
        throw new ArgumentNullException(nameof(environment));
      MetalEnvironment previous = this.environment;
      try {
        this.environment = environment;
        foreach (var statement in statements) {
          Execute(statement);
        }
      } finally {
        this.environment = previous;
      }
    }
  }
}

