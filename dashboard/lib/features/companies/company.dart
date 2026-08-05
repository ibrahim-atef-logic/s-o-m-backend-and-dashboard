class CompanyCredential {
  const CompanyCredential({
    required this.code,
    required this.name,
    required this.tenantId,
    required this.clientId,
    required this.clientSecret,
    required this.finOpsBaseUrl,
    required this.isActive,
    this.clientSecretMasked,
    this.updatedAt,
  });

  final String code;
  final String name;
  final String tenantId;
  final String clientId;
  final String clientSecret;
  final String? clientSecretMasked;
  final String finOpsBaseUrl;
  final bool isActive;
  final String? updatedAt;

  factory CompanyCredential.fromJson(Map<String, dynamic> json) {
    return CompanyCredential(
      code: json['code'] as String? ?? '',
      name: json['name'] as String? ?? '',
      tenantId: json['tenantId'] as String? ?? '',
      clientId: json['clientId'] as String? ?? '',
      clientSecret: json['clientSecret'] as String? ?? '',
      clientSecretMasked: json['clientSecretMasked'] as String?,
      finOpsBaseUrl: json['finOpsBaseUrl'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? true,
      updatedAt: json['updatedAt']?.toString(),
    );
  }

  Map<String, Object?> toBody() => <String, Object?>{
        'code': code,
        'name': name,
        'tenantId': tenantId,
        'clientId': clientId,
        'clientSecret': clientSecret,
        'finOpsBaseUrl': finOpsBaseUrl,
        'isActive': isActive,
      };
}
