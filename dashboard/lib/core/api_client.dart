import 'dart:convert';

import 'package:http/http.dart' as http;

class ApiClient {
  ApiClient({String? baseUrl})
      : baseUrl = baseUrl ??
            const String.fromEnvironment(
              'API_BASE_URL',
              defaultValue: 'http://localhost:3000',
            );

  final String baseUrl;

  Future<Map<String, dynamic>> getJson(String path) async {
    final http.Response res = await http.get(Uri.parse('$baseUrl$path'));
    return _decode(res);
  }

  Future<Map<String, dynamic>> postJson(String path, Map<String, Object?> body) async {
    final http.Response res = await http.post(
      Uri.parse('$baseUrl$path'),
      headers: <String, String>{'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );
    return _decode(res);
  }

  Future<Map<String, dynamic>> putJson(String path, Map<String, Object?> body) async {
    final http.Response res = await http.put(
      Uri.parse('$baseUrl$path'),
      headers: <String, String>{'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );
    return _decode(res);
  }

  Future<Map<String, dynamic>> deleteJson(String path) async {
    final http.Response res = await http.delete(Uri.parse('$baseUrl$path'));
    return _decode(res);
  }

  Map<String, dynamic> _decode(http.Response res) {
    final Object? raw = jsonDecode(res.body);
    if (raw is! Map<String, dynamic>) {
      throw Exception('Unexpected response');
    }
    if (res.statusCode >= 400 || raw['success'] == false) {
      final Object? err = raw['error'];
      final String msg = err is Map<String, dynamic>
          ? (err['message'] as String? ?? 'Request failed')
          : 'Request failed (${res.statusCode})';
      throw Exception(msg);
    }
    return raw;
  }
}
