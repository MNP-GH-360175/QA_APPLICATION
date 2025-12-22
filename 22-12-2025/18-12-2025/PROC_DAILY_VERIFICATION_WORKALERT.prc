CREATE OR REPLACE PROCEDURE PROC_DAILY_VERIFICATION_WORKALERT
AS
    v_today DATE := TRUNC(SYSDATE);
BEGIN
    -- Delete old alerts for today first (avoid duplicates if run multiple times)
    DELETE FROM mana0809.tbl_ma_common_alert
    WHERE module_id = 3444566
      AND TRUNC(entr_dt) = v_today
      AND alert_message LIKE 'CRF VERIFICATION PENDING%';

    -- Insert new alerts for testers who have pending CRFs today
    INSERT INTO mana0809.tbl_ma_common_alert (
        emp_code,
        module_id,
        entr_dt,
        alert_message,
        status
    )
    SELECT DISTINCT
        ets.emp_code,
        3444566,
        SYSDATE,
        'CRF VERIFICATION PENDING - ' || 
        LISTAGG(n.crf_id, ', ') WITHIN GROUP (ORDER BY n.crf_id) || 
        ' pending in your code. Kindly review and verify immediately.',
        1
    FROM mana0809.srm_dailyrelease_updn n
    JOIN mana0809.srm_test_assign ts ON ts.request_id = n.request_id
    JOIN mana0809.employee_master ets ON ts.assign_to = ets.emp_code
    WHERE TRUNC(n.updated_on) = v_today
      AND NOT EXISTS (
          SELECT 1
          FROM mana0809.tbl_release_verify v
          WHERE v.crf_id = n.crf_id
            AND TRUNC(v.release_dt) = v_today
            AND v.verify_status = 1
      )
    GROUP BY ets.emp_code;

    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
        RAISE;
END;
/
