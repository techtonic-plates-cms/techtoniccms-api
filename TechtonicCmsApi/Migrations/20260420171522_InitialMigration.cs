using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechtonicCmsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

             // CMS JSONB extraction functions for querying dynamic field values.
            // Each function extracts a typed value from a JSONB document by key.
            // IMMUTABLE = same inputs always produce same output (required for expression indexes).
            // STRICT = returns NULL automatically if any input is NULL (handles missing keys).
            migrationBuilder.Sql("""
                CREATE FUNCTION cms_extract_text(doc jsonb, key text) RETURNS text
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT doc->>key;
                  $$;

                CREATE FUNCTION cms_extract_number(doc jsonb, key text) RETURNS numeric
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::numeric;
                  $$;

                CREATE FUNCTION cms_extract_boolean(doc jsonb, key text) RETURNS boolean
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::boolean;
                  $$;

                CREATE FUNCTION cms_extract_datetime(doc jsonb, key text) RETURNS timestamptz
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::timestamptz;
                  $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS cms_extract_text(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_number(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_boolean(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_datetime(jsonb, text);
                """);
          
        }
    }
}
