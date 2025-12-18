CREATE OR REPLACE PROCEDURE PROC_DAILY_VERIFICATION_MASTER(
    p_flag IN VARCHAR2,
    p_from_date IN VARCHAR2,
    p_to_date IN VARCHAR2,
    p_release_type IN VARCHAR2 DEFAULT NULL,
    p_tester_tl IN VARCHAR2 DEFAULT NULL,
    p_tester_name IN VARCHAR2 DEFAULT NULL,
    p_json_input IN CLOB DEFAULT NULL,
    p_updated_by IN VARCHAR2 DEFAULT NULL,
    p_result OUT SYS_REFCURSOR
) AS
    v_from_dt DATE;
    v_to_dt DATE;

    TYPE t_item_rec IS RECORD(
        crf_id VARCHAR2(50),
        request_id VARCHAR2(50),
        release_date VARCHAR2(20),
        working_status VARCHAR2(100),
        remarks VARCHAR2(4000),
        attachment_name VARCHAR2(255),
        attachment_b64 CLOB,
        attachment_mime VARCHAR2(100)
    );
    TYPE t_item_tab IS TABLE OF t_item_rec;
    v_items t_item_tab := t_item_tab();

    v_release_dt DATE;
    v_actual_release_dt DATE;  -- New: actual release date from srm_dailyrelease_updn
    v_work_status NUMBER(1);
    v_blob BLOB;
    v_exists NUMBER;

    v_crf_name VARCHAR2(500);
    v_techlead VARCHAR2(200);
    v_developer VARCHAR2(500);
    v_tester_tl VARCHAR2(200);
    v_tester VARCHAR2(500);

    v_ErrID NUMBER;
    v_ErrDesc VARCHAR2(4000);
    v_ErrDtl CLOB;
BEGIN
    v_from_dt := TO_DATE(p_from_date, 'DD-MON-YYYY');
    v_to_dt := TO_DATE(p_to_date, 'DD-MON-YYYY');

    IF p_flag = 'FETCH' THEN
        OPEN p_result FOR
            WITH base_releases AS (
                SELECT DISTINCT
                    n.crf_id,
                    NVL(TO_CHAR(n.request_id), 'NO REQUEST ID') AS request_id,
                    sr.objective AS crf_name,
                    TO_CHAR(n.updated_on, 'DD-MON-YYYY') AS release_date,
                    e.emp_name AS techlead_name,
                    LISTAGG(DISTINCT re.emp_name, ',') WITHIN GROUP (ORDER BY re.emp_name) AS developer_name,
                    LISTAGG(DISTINCT ets.emp_name, ',') WITHIN GROUP (ORDER BY ets.emp_name) AS tester_name,
                    est.emp_name AS tester_tl_name,
                    DECODE(n.release_type, 1, 'Daily Release Report', 2, 'Exceptional & Expedite Report', 3, 'Expedite') AS release_type_text
                FROM mana0809.srm_dailyrelease_updn n
                LEFT JOIN mana0809.srm_software_request sr ON n.request_id = sr.request_id
                LEFT JOIN mana0809.srm_request_assign rq ON n.request_id = rq.request_id
                LEFT JOIN mana0809.employee_master re ON rq.assign_to = re.emp_code
                LEFT JOIN mana0809.employee_master e ON n.techlead_id = e.emp_code
                LEFT JOIN mana0809.srm_testing st ON st.request_id = n.request_id
                LEFT JOIN mana0809.employee_master est ON st.test_lead = est.emp_code
                LEFT JOIN mana0809.srm_test_assign ts ON ts.request_id = n.request_id
                LEFT JOIN mana0809.employee_master ets ON ts.assign_to = ets.emp_code
                WHERE TRUNC(n.updated_on) BETWEEN v_from_dt AND v_to_dt
                  AND (p_release_type IS NULL OR
                       (n.release_type = 1 AND p_release_type LIKE '%Daily%') OR
                       (n.release_type IN (2,3) AND p_release_type LIKE '%Exception%'))
                  AND (p_tester_tl IS NULL OR UPPER(est.emp_name) = UPPER(p_tester_tl))
                  AND (p_tester_name IS NULL OR EXISTS (
                        SELECT 1
                        FROM mana0809.srm_test_assign ts2
                        JOIN mana0809.employee_master ets2 ON ts2.assign_to = ets2.emp_code
                        WHERE ts2.request_id = n.request_id
                          AND UPPER(ets2.emp_name) = UPPER(p_tester_name)
                  ))
                GROUP BY n.crf_id, n.request_id, sr.objective, n.updated_on, e.emp_name, est.emp_name, n.release_type
            ),
            verification AS (
                SELECT v.crf_id,
                       TO_CHAR(v.release_dt, 'DD-MON-YYYY') AS release_date_str,
                       v.working_status,
                       v.remarks,
                       v.attachment_filename AS attachment_name,
                       v.updated_by AS verified_by,
                       TO_CHAR(v.updated_on, 'DD-MON-YYYY HH24:MI') AS verified_on
                FROM tbl_release_verify v
                WHERE TRUNC(v.release_dt) BETWEEN v_from_dt AND v_to_dt
            ),
            history_agg AS (
                SELECT rv.verify_id,
                       '[' ||
                       -- Current verification (always first/latest)
                       '{"status_text":"' ||
                       DECODE(rv.working_status,
                              1, 'Working', 2, 'Not Working', 3, 'In Progress',
                              4, 'Data need to capture', 5, 'User feedback pending',
                              6, 'On Hold', 'Pending') || '",' ||
                       '"remarks":"' || REPLACE(NVL(rv.remarks, ''), '"', '\"') || '",' ||
                       '"attachment_name":"' || NVL(rv.attachment_filename, '') || '",' ||
                       '"verified_by":"' || NVL(rv.updated_by, 'Unknown') || '",' ||
                       '"verified_on":"' || TO_CHAR(rv.updated_on, 'DD-MON-YYYY HH24:MI') || '"}' ||
                       NVL2(old_history.history_list, ',' || old_history.history_list, '') ||
                       ']' AS history_json
                FROM tbl_release_verify rv
                LEFT JOIN (
                    SELECT verify_id,
                           LISTAGG(
                             '{"status_text":"' ||
                             DECODE(old_working_status,
                                    1, 'Working', 2, 'Not Working', 3, 'In Progress',
                                    4, 'Data need to capture', 5, 'User feedback pending',
                                    6, 'On Hold', 'Unknown') || '",' ||
                             '"remarks":"' || REPLACE(NVL(old_remarks, ''), '"', '\"') || '",' ||
                             '"attachment_name":"' || NVL(old_attachment_filename, '') || '",' ||
                             '"verified_by":"' || NVL(changed_by, '') || '",' ||
                             '"verified_on":"' || TO_CHAR(changed_on, 'DD-MON-YYYY HH24:MI') || '"}',
                             ',') WITHIN GROUP (ORDER BY changed_on DESC) AS history_list
                    FROM tbl_release_verify_history
                    GROUP BY verify_id
                ) old_history ON rv.verify_id = old_history.verify_id
            )
            SELECT b.crf_id AS CRF_ID,
                   b.request_id AS REQUEST_ID,
                   b.crf_name AS CRF_NAME,
                   b.release_date AS RELEASE_DATE,
                   b.techlead_name AS TECHLEAD_NAME,
                   b.developer_name AS DEVELOPER_NAME,
                   b.tester_name AS TESTER_NAME,
                   b.tester_tl_name AS TESTER_TL_NAME,
                   b.release_type_text AS RELEASE_TYPE,
                   v.working_status AS WORKING_STATUS,
                   v.remarks AS REMARKS,
                   v.attachment_name AS ATTACHMENT_NAME,
                   v.verified_by AS VERIFIED_BY,
                   v.verified_on AS VERIFIED_ON,
                   NVL(h.history_json, '[]') AS HISTORY_JSON
            FROM base_releases b
            LEFT JOIN verification v
              ON b.crf_id = v.crf_id
             AND b.release_date = v.release_date_str
            LEFT JOIN tbl_release_verify rv
              ON rv.crf_id = b.crf_id
             AND TO_CHAR(rv.release_dt, 'DD-MON-YYYY') = b.release_date
            LEFT JOIN history_agg h
              ON rv.verify_id = h.verify_id
            ORDER BY b.release_date DESC, b.crf_id;

    ELSIF p_flag = 'SAVE' THEN
        SELECT jt.crf_id,
               jt.request_id,
               jt.release_date,
               jt.working_status,
               jt.remarks,
               jt.attachment_name,
               jt.attachment_b64,
               jt.attachment_mime
        BULK COLLECT INTO v_items
        FROM JSON_TABLE(p_json_input, '$[*]'
            COLUMNS(
                crf_id VARCHAR2 PATH '$.crfId',
                request_id VARCHAR2 PATH '$.requestId',
                release_date VARCHAR2 PATH '$.releaseDate',
                working_status VARCHAR2 PATH '$.workingStatus',
                remarks VARCHAR2 PATH '$.remarks',
                attachment_name VARCHAR2 PATH '$.attachmentName',
                attachment_b64 CLOB PATH '$.attachmentBase64',
                attachment_mime VARCHAR2 PATH '$.attachmentMime'
            )) jt;

        FOR i IN 1 .. v_items.COUNT LOOP
            -- Get the ACTUAL release date from srm_dailyrelease_updn (critical fix!)
            BEGIN
                SELECT TRUNC(n.updated_on)
                INTO v_actual_release_dt
                FROM mana0809.srm_dailyrelease_updn n
                WHERE n.crf_id = v_items(i).crf_id
                  AND ROWNUM = 1;
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    v_actual_release_dt := TRUNC(SYSDATE);
            END;

            -- Use client-sent release_date only as fallback (for display consistency)
            v_release_dt := v_actual_release_dt;
            IF v_items(i).release_date IS NOT NULL AND TRIM(v_items(i).release_date) IS NOT NULL THEN
                BEGIN
                    v_release_dt := TO_DATE(TRIM(UPPER(v_items(i).release_date)), 'DD-MON-YYYY');
                EXCEPTION
                    WHEN OTHERS THEN NULL;
                END;
            END IF;

            v_work_status := CASE UPPER(TRIM(v_items(i).working_status))
                               WHEN 'WORKING' THEN 1
                               WHEN 'NOT WORKING' THEN 2
                               WHEN 'IN PROGRESS' THEN 3
                               WHEN 'DATA NEED TO CAPTURE' THEN 4
                               WHEN 'USER FEEDBACK PENDING' THEN 5
                               WHEN 'ON HOLD' THEN 6
                               ELSE 1
                             END;

            v_blob := NULL;
            IF v_items(i).attachment_b64 IS NOT NULL THEN
                BEGIN
                    v_blob := BASE64_TO_BLOB(v_items(i).attachment_b64);
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END IF;

           
                 BEGIN
        SELECT sr.objective,
               NVL(e.emp_name, 'Unknown'),
               NVL(LISTAGG(DISTINCT re.emp_name, ',') WITHIN GROUP (ORDER BY re.emp_name), 'Unknown'),
               NVL(est.emp_name, 'Not Assigned'),
               NVL(LISTAGG(DISTINCT ets.emp_name, ',') WITHIN GROUP (ORDER BY ets.emp_name), 'Not Assigned')
          INTO v_crf_name, v_techlead, v_developer, v_tester_tl, v_tester
          FROM mana0809.srm_dailyrelease_updn n
          LEFT JOIN mana0809.srm_software_request sr ON n.request_id = sr.request_id
          LEFT JOIN mana0809.srm_request_assign rq ON n.request_id = rq.request_id
          LEFT JOIN mana0809.employee_master re ON rq.assign_to = re.emp_code
          LEFT JOIN mana0809.employee_master e ON n.techlead_id = e.emp_code
          LEFT JOIN mana0809.srm_testing st ON st.request_id = n.request_id
          LEFT JOIN mana0809.employee_master est ON st.test_lead = est.emp_code
          LEFT JOIN mana0809.srm_test_assign ts ON ts.request_id = n.request_id
          LEFT JOIN mana0809.employee_master ets ON ts.assign_to = ets.emp_code
         WHERE n.crf_id = v_items(i).crf_id
           AND TRUNC(n.updated_on) = v_actual_release_dt
         GROUP BY sr.objective, e.emp_name, est.emp_name;
      EXCEPTION
        WHEN NO_DATA_FOUND THEN
          v_crf_name := 'Unknown CRF';
          v_techlead := 'Unknown';
          v_developer := 'Unknown';
          v_tester_tl := 'Not Assigned';      -- Safe non-null value
          v_tester := 'Not Assigned';         -- Safe non-null value
        WHEN TOO_MANY_ROWS THEN
          v_crf_name := 'Multiple Records';
          v_techlead := 'Multiple';
          v_developer := 'Multiple';
          v_tester_tl := 'Not Assigned';
          v_tester := 'Not Assigned';
      END;

            SELECT COUNT(*) INTO v_exists
            FROM tbl_release_verify
            WHERE crf_id = v_items(i).crf_id
              AND TRUNC(release_dt) = v_actual_release_dt;

            IF v_exists > 0 THEN
                -- Archive old values
                INSERT INTO tbl_release_verify_history (
                    verify_id, old_remarks, old_working_status, old_attachment_filename,
                    changed_on, changed_by, change_reason
                )
                SELECT verify_id, remarks, working_status, attachment_filename,
                       SYSDATE, p_updated_by, 'Updated by Tester'
                FROM tbl_release_verify
                WHERE crf_id = v_items(i).crf_id
                  AND TRUNC(release_dt) = v_actual_release_dt;

                -- Update current
                UPDATE tbl_release_verify
                SET working_status = v_work_status,
                    remarks = NVL(v_items(i).remarks, remarks),
                    attachment = COALESCE(v_blob, attachment),
                    attachment_filename = NVL(v_items(i).attachment_name, attachment_filename),
                    attachment_mimetype = NVL(v_items(i).attachment_mime, attachment_mimetype),
                    updated_on = SYSDATE,
                    updated_by = p_updated_by,
                    verify_status = 1
                WHERE crf_id = v_items(i).crf_id
                  AND TRUNC(release_dt) = v_actual_release_dt;
            ELSE
                -- First time insert
                INSERT INTO tbl_release_verify (
                    crf_id, request_id, crf_name, release_dt,
                    techlead, developer, tester_tl, tester,
                    working_status, remarks,
                    attachment, attachment_filename, attachment_mimetype,
                    updated_on, updated_by, verify_status
                ) VALUES (
                    v_items(i).crf_id,
                    v_items(i).request_id,
                    v_crf_name,
                    v_actual_release_dt,
                    v_techlead,
                    v_developer,
                    v_tester_tl,
                    v_tester,
                    v_work_status,
                    NVL(v_items(i).remarks, 'No remarks provided'),
                    v_blob,
                    v_items(i).attachment_name,
                    v_items(i).attachment_mime,
                    SYSDATE,
                    p_updated_by,
                    1
                );
            END IF;
        END LOOP;

        OPEN p_result FOR SELECT 1 AS dummy FROM dual WHERE 1 = 0;

    END IF;

EXCEPTION
    WHEN OTHERS THEN
        v_ErrID := SEQ_Itproject_ERRORID.NEXTVAL;
        v_ErrDesc := SQLERRM;
        v_ErrDtl := SQLERRM || CHR(10) ||
                     DBMS_UTILITY.FORMAT_ERROR_BACKTRACE || CHR(10) ||
                     DBMS_UTILITY.FORMAT_ERROR_STACK || CHR(10) ||
                     DBMS_UTILITY.FORMAT_CALL_STACK;

        INSERT INTO TBL_ITPROJECTS_ERROR_DTL (
            error_id, proc_name, error_description, error_details, tra_dt
        ) VALUES (
            v_ErrID, 'PROC_DAILY_VERIFICATION_MASTER', v_ErrDesc, v_ErrDtl, SYSDATE
        );
        COMMIT;
        RAISE_APPLICATION_ERROR(-20001, 'Error in PROC_DAILY_VERIFICATION_MASTER: ' || v_ErrDesc);
END;
/
