import 'package:flutter/material.dart';

import 'company.dart';

class CompanyFormDialog extends StatefulWidget {
  const CompanyFormDialog({this.initial, super.key});

  final CompanyCredential? initial;

  @override
  State<CompanyFormDialog> createState() => _CompanyFormDialogState();
}

class _CompanyFormDialogState extends State<CompanyFormDialog> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  late final TextEditingController _code;
  late final TextEditingController _name;
  late final TextEditingController _tenant;
  late final TextEditingController _clientId;
  late final TextEditingController _clientSecret;
  late final TextEditingController _url;
  late bool _active;

  @override
  void initState() {
    super.initState();
    final CompanyCredential? i = widget.initial;
    _code = TextEditingController(text: i?.code ?? '');
    _name = TextEditingController(text: i?.name ?? '');
    _tenant = TextEditingController(text: i?.tenantId ?? '');
    _clientId = TextEditingController(text: i?.clientId ?? '');
    _clientSecret = TextEditingController(text: i?.clientSecret ?? '');
    _url = TextEditingController(text: i?.finOpsBaseUrl ?? '');
    _active = i?.isActive ?? true;
  }

  @override
  void dispose() {
    _code.dispose();
    _name.dispose();
    _tenant.dispose();
    _clientId.dispose();
    _clientSecret.dispose();
    _url.dispose();
    super.dispose();
  }

  String? _required(String? v) =>
      (v == null || v.trim().isEmpty) ? 'Required' : null;

  @override
  Widget build(BuildContext context) {
    final bool editing = widget.initial != null;
    return Dialog(
      insetPadding: const EdgeInsets.all(24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Form(
            key: _formKey,
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    editing ? 'Edit company' : 'Add company',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _code,
                    readOnly: editing,
                    decoration: const InputDecoration(
                      labelText: 'Company code',
                      hintText: 'logic-trial',
                    ),
                    validator: _required,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _name,
                    decoration: const InputDecoration(labelText: 'Display name'),
                    validator: _required,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _url,
                    decoration: const InputDecoration(
                      labelText: 'D365 / FinOps base URL',
                      hintText: 'https://….operations.eu.dynamics.com',
                    ),
                    validator: _required,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _tenant,
                    decoration: const InputDecoration(labelText: 'Azure Tenant ID'),
                    validator: _required,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _clientId,
                    decoration: const InputDecoration(labelText: 'Azure Client ID'),
                    validator: _required,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _clientSecret,
                    decoration: const InputDecoration(labelText: 'Azure Client Secret'),
                    validator: _required,
                    obscureText: true,
                  ),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Active (allows mobile login)'),
                    value: _active,
                    onChanged: (bool v) => setState(() => _active = v),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      TextButton(
                        onPressed: () => Navigator.pop(context),
                        child: const Text('Cancel'),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: () {
                          if (!(_formKey.currentState?.validate() ?? false)) {
                            return;
                          }
                          Navigator.pop(
                            context,
                            CompanyCredential(
                              code: _code.text.trim(),
                              name: _name.text.trim(),
                              tenantId: _tenant.text.trim(),
                              clientId: _clientId.text.trim(),
                              clientSecret: _clientSecret.text.trim(),
                              finOpsBaseUrl: _url.text.trim(),
                              isActive: _active,
                            ),
                          );
                        },
                        child: Text(editing ? 'Save' : 'Create'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
