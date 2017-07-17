﻿using System;
using Metal.FrontEnd.Scan;
using System.Collections.Generic;
using Metal.FrontEnd.Parse;
using Metal.Diagnostics.Runtime;
using Metal.FrontEnd.Interpret;

namespace Metal {

  public static class Metal {
    private static bool hadError = false;
    private static bool hadRuntimeError = false;
    private static Interpreter interpreter = new Interpreter();
    static void Main(string[] args) {
      foreach (var arg in args) {
        Console.WriteLine(arg);
      }
      if (args.Length > 1) {
        Console.WriteLine("Usage: metal [file]");
      } else if (args.Length == 1) {
          RunFile(args[0]);
      } else {
        RunPrompt();
      }
    }

    private static void Run(string source, bool isFile) {
      if (!hadError) {
        //Console.WriteLine(new ASTPrinter().Print(expression));
        try {
          Scanner scanner = new Scanner(source, isFile);
          List<Token> tokens = scanner.ScanTokens();
          Parser parser = new Parser(tokens);
          var statements = parser.Parse();
          interpreter.Interpret(statements);
        } catch (Exception error) {
        }
      } else Console.WriteLine("An error occurred.");
      // foreach (var token in tokens) {
      //   Console.WriteLine(token);
      // }
    }

    private static void RunFile(string source) {
      Run(source, true);
      if (hadError) Environment.Exit(65);
      if (hadRuntimeError) Environment.Exit(70);
    }

    private static void RunPrompt() {
      About.Print();
      for (;;) {
        Console.Write("> ");
        var line = Console.In.ReadLine();
        if (line == "clear") {
          hadError = false;
          Console.Clear();
          About.Print();
        } else Run(line, false);
      }
    }

    public static void Error(int line, string message) {
      Report(line, "", message);
    }

    public static void Error(Token token, string message) {
      if (token.Type == TokenType.EOF) {
        Report(token.Line, " at end", message);
      } else {
        Report(token.Line, " at '" + token.Lexeme + "'", message);
      }
    }
    public static void Report(int line, string where, string message) {
      Console.WriteLine(string.Format("[line {0}] Error {1}: {2}", line, where, message));
      hadError = true;
    }

    public static void RuntimeError(RuntimeError error) {
      Console.Error.WriteLine(error.Message + string.Format("\n[line {0}]", error.Token.Line));
      hadRuntimeError = true;
    }

    class About {
      public static String Author { get { return "Takeshi Iwana"; } }

      public static String Version { get { return "0.0.0"; } }

      public static String Name { get { return "Metal"; } }

      public static String License { get { return String.Format("MIT License \n{0}", Copyright); } }

      public static String Copyright { get { return String.Format("Copyright (c) {0} - {1} {2}", 2015, DateTime.Now.Year.ToString(), Author); } }

      public static void Print() {
        Console.WriteLine(About.Name);
        Console.WriteLine(About.Version);
        Console.WriteLine(About.License);
        Console.WriteLine();
      }
    }
  }
}
