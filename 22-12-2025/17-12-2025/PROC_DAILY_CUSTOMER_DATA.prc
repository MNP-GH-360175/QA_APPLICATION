CREATE OR REPLACE PROCEDURE PROC_DAILY_CUSTOMER_DATA(
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
    SELECT 
        COUNT(t.cust_id) AS total_customers,
        COUNT(hy.cust_id) AS hyperverge_customers,
        COUNT(jk.cust_id) AS jukshio_customers,
        COUNT(ds.cust_id) AS doorstep_customers,
        COUNT(ac.cust_id) AS aadhar_count,
        COUNT(dg.cust_id) AS digi_count,
        COUNT(oc.cust_id) AS other_id_count
    FROM mana0809.customer t
    LEFT JOIN (
        SELECT a.cust_id
        FROM mana0809.tbl_hyperverge_log a
        WHERE a.status = 'IN'
          AND a.cust_id IS NOT NULL
          AND TRUNC(a.tra_date) = TRUNC(SYSDATE)
    ) hy ON t.cust_id = hy.cust_id
    LEFT JOIN (
        SELECT c.cust_id
        FROM mana0809.tbl_jukshio_log c
        WHERE c.status = 'completed'
          AND c.cust_id IS NOT NULL
          AND TRUNC(c.tra_date) = TRUNC(SYSDATE)
    ) jk ON t.cust_id = jk.cust_id
    LEFT JOIN (
        SELECT g.cust_id
        FROM mana0809.tbl_hyperverge_log g
        WHERE g.status = 'IN'
          AND g.cust_id IS NOT NULL
          AND SUBSTR(g.ref_id, 6, 2) = 'DS'
          AND TRUNC(g.tra_date) = TRUNC(SYSDATE)
    ) ds ON t.cust_id = ds.cust_id
    LEFT JOIN (
        SELECT j.cust_id
        FROM mana0809.identity_dtl j
        WHERE j.identity_id IN (16, 505)
    ) ac ON t.cust_id = ac.cust_id
    LEFT JOIN (
        SELECT kk.cust_id FROM mana0809.tbl_jukshio_log kk WHERE kk.digilocker_status = 'YES'
        UNION ALL
        SELECT ll.cust_id FROM mana0809.tbl_hyperverge_log ll WHERE ll.digilocker_status = 'YES'
    ) dg ON t.cust_id = dg.cust_id
    LEFT JOIN (
        SELECT m.cust_id
        FROM mana0809.identity_dtl m
        WHERE m.identity_id NOT IN (16, 505)
    ) oc ON t.cust_id = oc.cust_id
    WHERE TRUNC(t.created_date) = TRUNC(SYSDATE)
      AND t.branch_id <> 0;
END;
/
