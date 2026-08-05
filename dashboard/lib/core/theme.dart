import 'package:flutter/material.dart';

ThemeData buildDashboardTheme(TextTheme textTheme) {
  const Color primary = Color(0xFF0B5F7D);
  const Color surface = Color(0xFFF3F6F8);
  return ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: primary,
      primary: primary,
      surface: Colors.white,
      brightness: Brightness.light,
    ),
    scaffoldBackgroundColor: surface,
    textTheme: textTheme.apply(
      bodyColor: const Color(0xFF1F2A33),
      displayColor: const Color(0xFF1F2A33),
    ),
    appBarTheme: const AppBarTheme(
      backgroundColor: Colors.white,
      foregroundColor: Color(0xFF0B5F7D),
      elevation: 0,
      centerTitle: false,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: primary,
        foregroundColor: Colors.white,
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
      ),
    ),
  );
}
