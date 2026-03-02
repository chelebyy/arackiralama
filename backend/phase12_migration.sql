CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE admin_users (
        id uuid NOT NULL,
        email character varying(160) NOT NULL,
        password_hash character varying(256) NOT NULL,
        full_name character varying(140) NOT NULL,
        role character varying(40) NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_admin_users" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE audit_logs (
        id uuid NOT NULL,
        action character varying(120) NOT NULL,
        entity_type character varying(120) NOT NULL,
        entity_id character varying(120) NOT NULL,
        user_id character varying(120),
        timestamp timestamp with time zone NOT NULL,
        details text NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_logs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE background_jobs (
        id uuid NOT NULL,
        type character varying(120) NOT NULL,
        payload text NOT NULL,
        status character varying(40) NOT NULL,
        retry_count integer NOT NULL,
        scheduled_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_background_jobs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE campaigns (
        id uuid NOT NULL,
        code character varying(64) NOT NULL,
        discount_type character varying(32) NOT NULL,
        discount_value numeric(18,2) NOT NULL,
        min_days integer NOT NULL,
        valid_from date NOT NULL,
        valid_until date NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_campaigns" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE customers (
        id uuid NOT NULL,
        full_name character varying(140) NOT NULL,
        phone character varying(40) NOT NULL,
        email character varying(160) NOT NULL,
        birth_date date,
        license_year integer NOT NULL,
        identity_number character varying(32) NOT NULL,
        nationality character varying(80) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_customers" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE feature_flags (
        id uuid NOT NULL,
        name character varying(120) NOT NULL,
        enabled boolean NOT NULL,
        description character varying(500) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_feature_flags" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE offices (
        id uuid NOT NULL,
        name character varying(120) NOT NULL,
        address character varying(250) NOT NULL,
        phone character varying(40) NOT NULL,
        is_airport boolean NOT NULL,
        opening_hours character varying(120) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_offices" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE payment_webhook_events (
        id uuid NOT NULL,
        provider_event_id character varying(120) NOT NULL,
        payload text NOT NULL,
        processed boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payment_webhook_events" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE vehicle_groups (
        id uuid NOT NULL,
        name_tr character varying(120) NOT NULL,
        name_en character varying(120) NOT NULL,
        name_ru character varying(120) NOT NULL,
        name_ar character varying(120) NOT NULL,
        name_de character varying(120) NOT NULL,
        deposit_amount numeric(18,2) NOT NULL,
        min_age integer NOT NULL,
        min_license_years integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_vehicle_groups" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE pricing_rules (
        id uuid NOT NULL,
        vehicle_group_id uuid NOT NULL,
        start_date date NOT NULL,
        end_date date NOT NULL,
        daily_price numeric(18,2) NOT NULL,
        multiplier numeric(8,4) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_pricing_rules" PRIMARY KEY (id),
        CONSTRAINT "FK_pricing_rules_vehicle_groups_vehicle_group_id" FOREIGN KEY (vehicle_group_id) REFERENCES vehicle_groups (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE vehicles (
        id uuid NOT NULL,
        plate character varying(32) NOT NULL,
        brand character varying(80) NOT NULL,
        model character varying(80) NOT NULL,
        year integer NOT NULL,
        color character varying(40) NOT NULL,
        group_id uuid NOT NULL,
        office_id uuid NOT NULL,
        status character varying(40) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_vehicles" PRIMARY KEY (id),
        CONSTRAINT "FK_vehicles_offices_office_id" FOREIGN KEY (office_id) REFERENCES offices (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_vehicles_vehicle_groups_group_id" FOREIGN KEY (group_id) REFERENCES vehicle_groups (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE reservations (
        id uuid NOT NULL,
        public_code character varying(24) NOT NULL,
        customer_id uuid NOT NULL,
        vehicle_id uuid NOT NULL,
        pickup_datetime timestamp with time zone NOT NULL,
        return_datetime timestamp with time zone NOT NULL,
        status character varying(32) NOT NULL,
        total_amount numeric(18,2) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_reservations" PRIMARY KEY (id),
        CONSTRAINT "FK_reservations_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_reservations_vehicles_vehicle_id" FOREIGN KEY (vehicle_id) REFERENCES vehicles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE payment_intents (
        id uuid NOT NULL,
        reservation_id uuid NOT NULL,
        amount numeric(18,2) NOT NULL,
        status character varying(40) NOT NULL,
        provider character varying(50) NOT NULL,
        idempotency_key character varying(120) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payment_intents" PRIMARY KEY (id),
        CONSTRAINT "FK_payment_intents_reservations_reservation_id" FOREIGN KEY (reservation_id) REFERENCES reservations (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE TABLE reservation_holds (
        id uuid NOT NULL,
        reservation_id uuid NOT NULL,
        vehicle_id uuid NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        session_id character varying(120) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_reservation_holds" PRIMARY KEY (id),
        CONSTRAINT "FK_reservation_holds_reservations_reservation_id" FOREIGN KEY (reservation_id) REFERENCES reservations (id) ON DELETE CASCADE,
        CONSTRAINT "FK_reservation_holds_vehicles_vehicle_id" FOREIGN KEY (vehicle_id) REFERENCES vehicles (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    INSERT INTO feature_flags (id, created_at, description, enabled, name, updated_at)
    VALUES ('33333333-3333-3333-3333-333333333331', TIMESTAMPTZ '2026-03-02T00:00:00Z', 'Online payment provider integration toggle', FALSE, 'EnableOnlinePayment', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    INSERT INTO feature_flags (id, created_at, description, enabled, name, updated_at)
    VALUES ('33333333-3333-3333-3333-333333333332', TIMESTAMPTZ '2026-03-02T00:00:00Z', 'Campaign and discount rules toggle', TRUE, 'EnableCampaigns', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    INSERT INTO offices (id, address, created_at, is_airport, name, opening_hours, phone, updated_at)
    VALUES ('11111111-1111-1111-1111-111111111111', 'Sekerhane Mah. Ataturk Blv. No:10 Alanya/Antalya', TIMESTAMPTZ '2026-03-02T00:00:00Z', FALSE, 'Alanya Merkez', '08:00-22:00', '+90 242 000 00 01', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    INSERT INTO offices (id, address, created_at, is_airport, name, opening_hours, phone, updated_at)
    VALUES ('11111111-1111-1111-1111-111111111112', 'Gazipasa-Alanya Havalimani Terminal Ici', TIMESTAMPTZ '2026-03-02T00:00:00Z', TRUE, 'Gazipasa Airport', '24/7', '+90 242 000 00 02', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    INSERT INTO vehicle_groups (id, created_at, deposit_amount, min_age, min_license_years, name_ar, name_de, name_en, name_ru, name_tr, updated_at)
    VALUES ('22222222-2222-2222-2222-222222222221', TIMESTAMPTZ '2026-03-02T00:00:00Z', 2000.0, 21, 2, 'Economy', 'Economy', 'Economy', 'Economy', 'Ekonomi', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    INSERT INTO vehicle_groups (id, created_at, deposit_amount, min_age, min_license_years, name_ar, name_de, name_en, name_ru, name_tr, updated_at)
    VALUES ('22222222-2222-2222-2222-222222222222', TIMESTAMPTZ '2026-03-02T00:00:00Z', 3500.0, 25, 3, 'SUV', 'SUV', 'SUV', 'SUV', 'SUV', TIMESTAMPTZ '2026-03-02T00:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX "IX_admin_users_email" ON admin_users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX "IX_campaigns_code" ON campaigns (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_customers_email" ON customers (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_customers_identity_number" ON customers (identity_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX "IX_feature_flags_name" ON feature_flags (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_payment_intents_reservation_id" ON payment_intents (reservation_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX idx_payment_idempotency ON payment_intents (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX idx_webhook_provider_event ON payment_webhook_events (provider_event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_pricing_rules_vehicle_group_id" ON pricing_rules (vehicle_group_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_pricing_date_range ON pricing_rules (start_date, end_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_reservation_holds_reservation_id" ON reservation_holds (reservation_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_reservation_holds_vehicle_id" ON reservation_holds (vehicle_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_holds_expires ON reservation_holds (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_holds_session ON reservation_holds (session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_reservations_customer_id" ON reservations (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX "IX_reservations_public_code" ON reservations (public_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_reservations_vehicle_dates ON reservations (vehicle_id, pickup_datetime, return_datetime);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_reservations_active_dates ON reservations (vehicle_id, pickup_datetime, return_datetime) WHERE status IN ('Paid','Active');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_reservations_status_created ON reservations (status, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX "IX_vehicles_group_id" ON vehicles (group_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE UNIQUE INDEX "IX_vehicles_plate" ON vehicles (plate);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_vehicles_available ON vehicles (office_id, group_id, status) WHERE status = 'Available';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    CREATE INDEX idx_vehicles_office_status_group ON vehicles (office_id, status, group_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260302082825_Phase12DatabaseSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260302082825_Phase12DatabaseSchema', '10.0.0-rc.2.25502.107');
    END IF;
END $EF$;
COMMIT;

