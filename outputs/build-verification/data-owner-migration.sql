BEGIN TRANSACTION;
ALTER TABLE [import_data_quality_issues] ADD [requires_data_owner] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [import_data_quality_issues] ADD [referred_at] datetime2 NULL;

ALTER TABLE [import_data_quality_issues] ADD [referred_by_user_id] nvarchar(450) NULL;

ALTER TABLE [import_data_quality_issues] ADD [referred_to] nvarchar(200) NULL;

ALTER TABLE [import_data_quality_issues] ADD [response_due_at] datetime2 NULL;

ALTER TABLE [import_data_quality_issues] ADD [referral_question] nvarchar(2000) NULL;

ALTER TABLE [import_data_quality_issues] ADD [data_owner_response] nvarchar(2000) NULL;

ALTER TABLE [import_data_quality_issues] ADD [responded_at] datetime2 NULL;

ALTER TABLE [import_data_quality_issues] ADD [response_recorded_by_user_id] nvarchar(450) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260830204039_AddImportDataOwnerConsultation', N'9.0.0');

COMMIT;
GO

