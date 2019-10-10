﻿using System;

// Uses the same method names as the Unity logger
public class Debug {
    public static void Log(string message) {
        Log(message, ConsoleColor.White);
    }

    public static void LogWarning(string warning) {
        Log(warning, ConsoleColor.Yellow);
    }

    public static void LogError(string error) {
        Log(error, ConsoleColor.Red);
    }

    private static void Log(string message, ConsoleColor color) {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = ConsoleColor.White;
        
    }
}