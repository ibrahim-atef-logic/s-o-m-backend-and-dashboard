import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import 'companies_api.dart';
import 'company.dart';
import 'company_form_dialog.dart';

class CompaniesPage extends StatefulWidget {
  const CompaniesPage({super.key});

  @override
  State<CompaniesPage> createState() => _CompaniesPageState();
}

class _CompaniesPageState extends State<CompaniesPage> {
  final CompaniesApi _api = CompaniesApi(ApiClient());
  late Future<List<CompanyCredential>> _future;

  @override
  void initState() {
    super.initState();
    _future = _api.list();
  }

  void _reload() {
    setState(() {
      _future = _api.list();
    });
  }

  Future<void> _openForm({CompanyCredential? existing}) async {
    final CompanyCredential? result = await showDialog<CompanyCredential>(
      context: context,
      barrierDismissible: false,
      builder: (BuildContext context) => CompanyFormDialog(initial: existing),
    );
    if (result == null) {
      return;
    }
    try {
      if (existing == null) {
        await _api.create(result);
      } else {
        await _api.update(result);
      }
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(existing == null ? 'Company created' : 'Company updated')),
      );
      _reload();
    } catch (e) {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString()), backgroundColor: const Color(0xFFC0281F)),
      );
    }
  }

  Future<void> _delete(CompanyCredential company) async {
    final bool? ok = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: const Text('Delete company?'),
        content: Text('Remove "${company.code}" from the registry? Mobile login will fail for it.'),
        actions: <Widget>[
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Delete')),
        ],
      ),
    );
    if (ok != true) {
      return;
    }
    try {
      await _api.delete(company.code);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Company deleted')),
      );
      _reload();
    } catch (e) {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString()), backgroundColor: const Color(0xFFC0281F)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Logic Retail · Super Admin'),
        actions: <Widget>[
          IconButton(
            tooltip: 'Refresh',
            onPressed: _reload,
            icon: const Icon(Icons.refresh),
          ),
          const SizedBox(width: 8),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _openForm(),
        icon: const Icon(Icons.add),
        label: const Text('Add company'),
      ),
      body: FutureBuilder<List<CompanyCredential>>(
        future: _future,
        builder: (BuildContext context, AsyncSnapshot<List<CompanyCredential>> snap) {
          if (snap.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(snap.error.toString(), textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                    FilledButton(onPressed: _reload, child: const Text('Retry')),
                  ],
                ),
              ),
            );
          }
          final List<CompanyCredential> rows = snap.data ?? <CompanyCredential>[];
          if (rows.isEmpty) {
            return const Center(child: Text('No companies yet. Add one to enable mobile login.'));
          }
          return LayoutBuilder(
            builder: (BuildContext context, BoxConstraints c) {
              final bool wide = c.maxWidth >= 960;
              return ListView.separated(
                padding: EdgeInsets.fromLTRB(wide ? 48 : 16, 16, wide ? 48 : 16, 100),
                itemCount: rows.length + 1,
                separatorBuilder: (_, __) => const SizedBox(height: 12),
                itemBuilder: (BuildContext context, int index) {
                  if (index == 0) {
                    return Text(
                      'Companies registered here are required for mobile login. Store D365 app registration secrets per legal entity / trial host.',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: const Color(0xFF4B5A66),
                          ),
                    );
                  }
                  final CompanyCredential company = rows[index - 1];
                  return _CompanyTile(
                    company: company,
                    onEdit: () => _openForm(existing: company),
                    onDelete: () => _delete(company),
                  );
                },
              );
            },
          );
        },
      ),
    );
  }
}

class _CompanyTile extends StatelessWidget {
  const _CompanyTile({
    required this.company,
    required this.onEdit,
    required this.onDelete,
  });

  final CompanyCredential company;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: const Color(0xFFDCEFF6),
                borderRadius: BorderRadius.circular(10),
              ),
              child: const Icon(Icons.apartment_outlined, color: Color(0xFF0B5F7D)),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Text(
                        company.code,
                        style: Theme.of(context).textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                      const SizedBox(width: 8),
                      if (!company.isActive)
                        const Chip(
                          label: Text('Inactive'),
                          visualDensity: VisualDensity.compact,
                        ),
                    ],
                  ),
                  Text(company.name),
                  const SizedBox(height: 6),
                  Text(
                    company.finOpsBaseUrl,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  Text(
                    'Tenant ${company.tenantId} · Client ${company.clientId}',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: const Color(0xFF6B7A87),
                        ),
                  ),
                ],
              ),
            ),
            IconButton(onPressed: onEdit, icon: const Icon(Icons.edit_outlined)),
            IconButton(onPressed: onDelete, icon: const Icon(Icons.delete_outline)),
          ],
        ),
      ),
    );
  }
}
