BEGIN TRANSACTION READ ONLY;
SELECT
    count(*) AS customer_rows,
    count(*) FILTER (WHERE nullif(btrim(identity_number), '') IS NOT NULL) AS identity_number_present,
    count(*) FILTER (WHERE birth_date IS NOT NULL) AS birth_date_present,
    count(*) FILTER (WHERE license_year <> 0) AS license_year_present
FROM customers;
SELECT
    count(*) AS reservation_rows,
    count(*) FILTER (WHERE driver_date_of_birth IS NOT NULL) AS driver_birth_date_present,
    count(*) FILTER (WHERE nullif(btrim(driver_license_number), '') IS NOT NULL) AS licence_number_present,
    count(*) FILTER (WHERE driver_license_issue_date IS NOT NULL) AS licence_issue_date_present,
    count(*) FILTER (WHERE driver_license_expiry_date IS NOT NULL) AS licence_expiry_date_present
FROM reservations;
COMMIT;
