import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'core/theme.dart';
import 'features/companies/companies_page.dart';

class DashboardApp extends StatelessWidget {
  const DashboardApp({super.key});

  @override
  Widget build(BuildContext context) {
    final TextTheme textTheme = GoogleFonts.sourceSans3TextTheme();
    return MaterialApp(
      title: 'Logic Retail Admin',
      debugShowCheckedModeBanner: false,
      theme: buildDashboardTheme(textTheme),
      home: const CompaniesPage(),
    );
  }
}
