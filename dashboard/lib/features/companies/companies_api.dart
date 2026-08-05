import '../../core/api_client.dart';
import 'company.dart';

class CompaniesApi {
  CompaniesApi(this._client);

  final ApiClient _client;

  Future<List<CompanyCredential>> list() async {
    final Map<String, dynamic> json = await _client.getJson('/api/v1/admin/companies');
    final Object? data = json['data'];
    if (data is! List<dynamic>) {
      return <CompanyCredential>[];
    }
    return data
        .whereType<Map<String, dynamic>>()
        .map(CompanyCredential.fromJson)
        .toList();
  }

  Future<CompanyCredential> create(CompanyCredential company) async {
    final Map<String, dynamic> json =
        await _client.postJson('/api/v1/admin/companies', company.toBody());
    return CompanyCredential.fromJson(json['data'] as Map<String, dynamic>);
  }

  Future<CompanyCredential> update(CompanyCredential company) async {
    final Map<String, dynamic> json = await _client.putJson(
      '/api/v1/admin/companies/${Uri.encodeComponent(company.code)}',
      company.toBody(),
    );
    return CompanyCredential.fromJson(json['data'] as Map<String, dynamic>);
  }

  Future<void> delete(String code) async {
    await _client.deleteJson('/api/v1/admin/companies/${Uri.encodeComponent(code)}');
  }
}
