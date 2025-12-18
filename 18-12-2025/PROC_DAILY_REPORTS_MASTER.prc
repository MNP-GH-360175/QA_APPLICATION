CREATE OR REPLACE PROCEDURE PROC_DAILY_REPORTS_MASTER (
    p_flag          IN VARCHAR2,
    p_sub_flag      IN VARCHAR2 DEFAULT NULL,
    p_from_date     IN VARCHAR2 DEFAULT NULL,
    p_to_date       IN VARCHAR2 DEFAULT NULL,
    p_release_type  IN VARCHAR2 DEFAULT NULL,
    p_tester_tl     IN VARCHAR2 DEFAULT NULL,
    p_tester_name   IN VARCHAR2 DEFAULT NULL,
    p_json_input    IN CLOB     DEFAULT NULL,
    p_updated_by    IN VARCHAR2 DEFAULT NULL,
    p_result        OUT SYS_REFCURSOR
) AS
    v_from_dt DATE;
    v_to_dt   DATE;
    v_flag    VARCHAR2(50);
BEGIN
    -- Normalize flag
    v_flag := UPPER(TRIM(p_flag));

    -- Safe date conversion with error handling
    IF TRIM(p_from_date) IS NOT NULL THEN
        BEGIN
            v_from_dt := TO_DATE(TRIM(p_from_date), 'DD-MON-YYYY');
        EXCEPTION
            WHEN OTHERS THEN
                OPEN p_result FOR
                    SELECT 'ERROR: Invalid p_from_date format. Expected DD-MON-YYYY' AS error_message FROM dual;
                RETURN;
        END;
    END IF;

    IF TRIM(p_to_date) IS NOT NULL THEN
        BEGIN
            v_to_dt := TO_DATE(TRIM(p_to_date), 'DD-MON-YYYY');
        EXCEPTION
            WHEN OTHERS THEN
                OPEN p_result FOR
                    SELECT 'ERROR: Invalid p_to_date format. Expected DD-MON-YYYY' AS error_message FROM dual;
                RETURN;
        END;
    END IF;

    -- Main routing logic
    CASE v_flag
        WHEN 'DAILY_VERIFICATION' THEN
            PROC_DAILY_VERIFICATION_MASTER(
                p_flag         => p_sub_flag,
                p_from_date    => p_from_date,
                p_to_date      => p_to_date,
                p_release_type => p_release_type,
                p_tester_tl    => p_tester_tl,
                p_tester_name  => p_tester_name,
                p_json_input   => p_json_input,
                p_updated_by   => p_updated_by,
                p_result       => p_result
            );

        WHEN 'KYC_DEVIATION' THEN
            IF v_from_dt IS NULL OR v_to_dt IS NULL THEN
                OPEN p_result FOR
                    SELECT 'ERROR: p_from_date and p_to_date are required for KYC_DEVIATION' AS error_message FROM dual;
                RETURN;
            END IF;

            proc_KYC_DEVIATION_REPORT(
                p_from_date => v_from_dt,
                p_to_date   => v_to_dt,
                p_cursor    => p_result
            );

        WHEN 'DAILY_CUSTOMER_DATA' THEN
            PROC_DAILY_CUSTOMER_DATA(
                p_cursor => p_result
            );

        ELSE
            OPEN p_result FOR
                SELECT 'ERROR: Invalid p_flag = ''' || NVL(p_flag, 'NULL') || 
                       '''. Valid values: DAILY_VERIFICATION, KYC_DEVIATION, DAILY_CUSTOMER_DATA'
                AS error_message FROM dual;
            RETURN;
    END CASE;



END PROC_DAILY_REPORTS_MASTER;
/
