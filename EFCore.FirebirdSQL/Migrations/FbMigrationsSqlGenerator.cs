/*                 
 *                    EntityFrameworkCore.FirebirdSQL
 *     
*
 *              
 *     Permission to use, copy, modify, and distribute this software and its
 *     documentation for any purpose, without fee, and without a written
 *     agreement is hereby granted, provided that the above copyright notice
 *     and this paragraph and the following two paragraphs appear in all copies. 
 * 
 *     The contents of this file are subject to the Initial
 *     Developer's Public License Version 1.0 (the "License");
 *     you may not use this file except in compliance with the
 *     License.
*
 *
 *     Software distributed under the License is distributed on
 *     an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *     express or implied.  See the License for the specific
 *     language governing rights and limitations under the License.
 *
 *      Credits: Rafael Almeida (ralms@ralms.net)
 *                              Sergipe-Brazil
 *
 *
 *                              
 *                  All Rights Reserved.
 */

using System;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using FirebirdSql.Data.FirebirdClient;

namespace Microsoft.EntityFrameworkCore.Migrations
{
	public class FbMigrationsSqlGenerator : MigrationsSqlGenerator
	{
		private IFbOptions _options { get; set; }
		public FbMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IFbOptions options)
			: base(dependencies)
		{
			_options = options;
		}

		protected override void Generate(MigrationOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			if (operation is FbCreateDatabaseOperation createDatabaseOperation)
			{
				Generate(createDatabaseOperation, model, builder);
				builder.EndCommand();

				using (var connection = new FbConnection(createDatabaseOperation.connectionStrBuilder.ToString()))
				{
					connection.Open();
				}
				return;
			}

			var dropDatabaseOperation = operation as FbDropDatabaseOperation;
			if (dropDatabaseOperation is FbDropDatabaseOperation)
			{
				Generate(dropDatabaseOperation, model, builder);
				builder.EndCommand();
				return;
			}

			base.Generate(operation, model, builder);
		}

		protected override void Generate( CreateTableOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate)
		{
			base.Generate(operation, model, builder, false);
			if (terminate)
			{
				builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
				EndStatement(builder);
			}

			if (_options.Settings.IsSupportIdentityIncrement)
				return;

			foreach (var column in operation.Columns.Where(p => !p.IsNullable))
			{
				var colAnnotation = (IAnnotation)column.FindAnnotation(FbAnnotationNames.ValueGenerationStrategy);
				if (colAnnotation == null)
					continue;

				var valueGenerationStrategy = colAnnotation.Value as FbValueGenerationStrategy?;
				if (valueGenerationStrategy == FbValueGenerationStrategy.IdentityColumn
					&& string.IsNullOrWhiteSpace(column.DefaultValueSql)
					&& column.DefaultValue == null)
				{

					var nameSequence = $"{column.Table}_{column.Name}";
					builder.AppendLine("EXECUTE BLOCK");
					builder.AppendLine("AS");
					builder.AppendLine("BEGIN");
					builder.Append("if (not exists(select 1 from rdb$generators where rdb$generator_name = '");
					builder.Append(nameSequence);
					builder.Append("')) then");
					builder.AppendLine();
					builder.AppendLine("begin");
					builder.AppendLine("\n");
					builder.Append("execute statement 'create sequence ");
					builder.Append(nameSequence);
					builder.Append("';");
					builder.AppendLine();
					builder.AppendLine("end");
					builder.AppendLine("END");


					var nameGenerator = $"{column.Table}_{column.Name}";
					builder.Append("CREATE GENERATOR ")
						   .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(nameGenerator))
						   .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
					EndStatement(builder);

					builder.Append("CREATE OR ALTER TRIGGER ")
						   .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(nameGenerator))
						   .Append(" FOR ")
						   .AppendLine(Dependencies.SqlGenerationHelper.DelimitIdentifier(column.Table))
						   .AppendLine("ACTIVE BEFORE INSERT POSITION 0 AS BEGIN")
						   .Append($"    IF(new.{Dependencies.SqlGenerationHelper.DelimitIdentifier(column.Name)} IS NULL) THEN")
						   .Append($"       new.{Dependencies.SqlGenerationHelper.DelimitIdentifier(column.Name)} ")
						   .AppendLine($"= GEN_ID({Dependencies.SqlGenerationHelper.DelimitIdentifier(nameGenerator)},1);")
						   .AppendLine("END;");

					EndStatement(builder);

				}
			}
		}

		protected override void Generate(DropColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			var identifier = Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema);
			var alterBase = $"ALTER TABLE {identifier} DROP {Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name)}";
			builder.Append(alterBase)
				   .Append(Dependencies.SqlGenerationHelper.StatementTerminator);

			EndStatement(builder);
		}

		protected override void Generate(AlterColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			var type = operation.ColumnType.ToUpper();
			if (operation.ColumnType == null)
			{
				var property = FindProperty(model, operation.Schema, operation.Table, operation.Name);

				type = property != null
					? Dependencies.TypeMapper.GetMapping(property).StoreType
					: Dependencies.TypeMapper.GetMapping(operation.ClrType).StoreType;
			}
			var identifier = Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema);
			builder.Append($"ALTER TABLE {identifier} ALTER COLUMN ");
			builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

			builder.Append(" TYPE ")
				   .Append(type)
				   .Append(operation.IsNullable ? "" : " NOT NULL")
				   .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);

			if (!type.StartsWith("BLOB", StringComparison.Ordinal))
			{
				builder.Append($"ALTER TABLE {identifier} ALTER COLUMN ");
				builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

				if (operation.DefaultValue != null)
				{
					var typeMapping = Dependencies.TypeMapper.GetMapping(operation.DefaultValue.GetType());
					builder.Append(" SET DEFAULT ")
						.Append(typeMapping.GenerateSqlLiteral(operation.DefaultValue))
						.AppendLine(Dependencies.SqlGenerationHelper.BatchTerminator);
				}
				else if (!string.IsNullOrWhiteSpace(operation.DefaultValueSql))
				{
					builder.Append(" SET DEFAULT ")
						.Append(operation.DefaultValueSql)
						.AppendLine(Dependencies.SqlGenerationHelper.BatchTerminator);
				}
				else
				{
					builder.Append(" DROP DEFAULT;");
				}
			}

			EndStatement(builder);
		}

		protected override void Generate(CreateSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			throw new NotImplementedException("The create sequence feature is not yet implemented.");
		}

		protected override void Generate(RenameIndexOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			throw new NotImplementedException("The RenameIndexOperation feature is not yet implemented.");
		}

		protected override void Generate(RenameSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			//Tanks Jean!
			builder.AppendLine("EXECUTE BLOCK")
			       .AppendLine("AS")
			       .AppendLine("DECLARE val INT = 0;")
			       .AppendLine("BEGIN")
			       .AppendLine($"SELECT GEN_ID({operation.Name}, 0) FROM RDB$DATABASE INTO :val;");

			if (_options.Settings.ServerVersion.Major >= 2)
			{
				builder.AppendLine($"EXECUTE STATEMENT 'CREATE SEQUENCE {Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName)}';")
					   .AppendLine($"EXECUTE STATEMENT 'ALTER SEQUENCE {Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName)} RESTART WITH ' || :val;");
			}
			else
			{
				builder.AppendLine($"EXECUTE STATEMENT 'CREATE GENERATOR {Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName)}';")
					   .AppendLine($"EXECUTE STATEMENT 'SET GENERATOR {Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName)} TO ' || :val;");
			}

			builder.AppendLine("END");
		}

		protected override void Generate(RenameTableOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			throw new NotImplementedException("The rename table feature is not yet implemented.");
		}

		protected override void Generate(CreateIndexOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate)
		{
			var method = (string)operation[FbAnnotationNames.Prefix];

			builder.Append("CREATE ");

			if (operation.IsUnique)
			{
				builder.Append("UNIQUE ");
			}

			builder.Append("INDEX ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
			       .Append(" ON ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));

			if (method != null)
			{
				builder.Append(" USING ")
				       .Append(method);
			}

			builder.Append(" (")
			       .Append(ColumnList(operation.Columns))
			       .Append(")");

			if (terminate)
			{
				builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
				EndStatement(builder);
			}
		}

		protected override void Generate( CreateIndexOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			Generate(operation, model, builder, true);
		}

		protected override void Generate(EnsureSchemaOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			throw new NotImplementedException("This feature is not yet implemented.");
		}

		public virtual void Generate(FbCreateDatabaseOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			var stringConnection = operation.connectionStrBuilder.ToString();
			FbConnection.CreateDatabase(stringConnection);
		}

		public virtual void Generate(FbDropDatabaseOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			FbConnection.ClearAllPools();
			FbConnection.DropDatabase(operation.ConnectionStringBuilder.ToString());
		}

		protected override void Generate(DropIndexOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER TABLE ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
			       .Append(" DROP CONSTRAINT ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
			       .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);

			EndStatement(builder);
		}

		protected override void Generate(RenameColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER TABLE ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
			       .Append(" ALTER ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
			       .Append(" TO ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName));

			EndStatement(builder);
		}

		protected override void ColumnDefinition(
			string schema,
			string table,
			string name,
			Type clrType,
			string type,
			bool? unicode,
			int? maxLength,
			bool rowVersion,
			bool nullable,
			object defaultValue,
			string defaultValueSql,
			string computedColumnSql,
			IAnnotatable annotatable,
			IModel model,
			MigrationCommandListBuilder builder)
		{
			var Identity = false;
			var valueGenerationStrategy = annotatable[FbAnnotationNames.ValueGenerationStrategy] as FbValueGenerationStrategy?;
			if ((valueGenerationStrategy == FbValueGenerationStrategy.IdentityColumn) && string.IsNullOrWhiteSpace(defaultValueSql) && defaultValue == null)
			{
				switch (type)
				{
					case "INTEGER":
					case "BIGINT":
						Identity = true;
						break;
					case "DATETIME":
					case "TIMESTAMP":
						defaultValueSql = $"CURRENT_TIMESTAMP";
						break;
				}
			}
			string onUpdateSql = null;
			if (valueGenerationStrategy == FbValueGenerationStrategy.ComputedColumn)
			{
				switch (type)
				{
					case "DATETIME":
					case "TIMESTAMP":
						if (string.IsNullOrWhiteSpace(defaultValueSql) && defaultValue == null)
							defaultValueSql = $"CURRENT_TIMESTAMP";
						onUpdateSql = $"CURRENT_TIMESTAMP";
						break;
				}
			}

			builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name))
			       .Append(" ")
			       .Append(type ?? GetColumnType(schema, table, name, clrType, unicode, maxLength, rowVersion, model));

			if (!nullable && Identity)
			{
				if (_options.Settings.IsSupportIdentityIncrement)
					builder.Append(" GENERATED BY DEFAULT AS IDENTITY NOT NULL");
				else
					builder.Append(" NOT NULL");
			}
			else
			{
				if (!nullable)
					builder.Append(" NOT NULL");

				if (defaultValueSql != null)
				{
					builder.Append(" DEFAULT ")
					       .Append(defaultValueSql);
				}
				else if (defaultValue != null)
				{
					var defaultValueLiteral = Dependencies.TypeMapper.GetMapping(clrType);
					builder.Append(" DEFAULT ")
					       .Append(defaultValueLiteral.GenerateSqlLiteral(defaultValue));
				}

				if (onUpdateSql != null)
				{
					builder.Append(" ON UPDATE ")
					       .Append(onUpdateSql);
				}
			}

		}

		protected override void DefaultValue(object defaultValue, string defaultValueSql, MigrationCommandListBuilder builder)
		{
			if (defaultValueSql != null)
			{
				builder.Append(" DEFAULT ")
				       .Append(defaultValueSql);
			}
			else if (defaultValue != null)
			{
				var typeMapping = Dependencies.TypeMapper.GetMapping(defaultValue.GetType());
				builder.Append(" DEFAULT ")
				       .Append(typeMapping.GenerateSqlLiteral(defaultValue));
			}
		}

		protected override void Generate(DropForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER TABLE ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
			       .Append(" DROP CONSTRAINT ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
			       .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);

			EndStatement(builder);
		}

		protected override void Generate(AddPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER TABLE ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
			       .Append(" ADD ");

			PrimaryKeyConstraint(operation, model, builder);
			builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);

			EndStatement(builder);
		}

		protected override void Generate(DropPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER TABLE ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
			       .Append(" DROP CONSTRAINT ")
			       .Append(operation.Name);

			EndStatement(builder);
		}

		public virtual void Rename(string schema, string name, string newName, string type, MigrationCommandListBuilder builder)
		{
			builder.Append("ALTER ")
			       .Append(type)
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name, schema))
			       .Append(" TO ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(newName, schema));
		} 

		protected override void ForeignKeyAction(ReferentialAction referentialAction, MigrationCommandListBuilder builder)
		{ 
			if (referentialAction == ReferentialAction.Restrict)
				builder.Append("NO ACTION");
			else
				base.ForeignKeyAction(referentialAction, builder);

		}

		protected override void ForeignKeyConstraint(AddForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder)
		{  
			if (operation.Name != null)
			{
				builder.Append("CONSTRAINT ")
				       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
				       .Append(" ");
			}

			builder.Append("FOREIGN KEY (")
			       .Append(ColumnList(operation.Columns))
			       .Append(") REFERENCES ")
			       .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.PrincipalTable,
			                                                                  operation.PrincipalSchema));

			if (operation.PrincipalColumns != null)
			{
				builder.Append(" (")
				       .Append(ColumnList(operation.PrincipalColumns))
				       .Append(")");
			}

			if (operation.OnUpdate != ReferentialAction.NoAction)
			{
				builder.Append(" ON UPDATE ");
				ForeignKeyAction(operation.OnUpdate, builder);
			}

			if (operation.OnDelete != ReferentialAction.NoAction)
			{
				builder.Append(" ON DELETE ");
				ForeignKeyAction(operation.OnDelete, builder);
			}
		}

		protected override string ColumnList(string[] columns) => string.Join(", ", columns.Select(Dependencies.SqlGenerationHelper.DelimitIdentifier));
	}
}
