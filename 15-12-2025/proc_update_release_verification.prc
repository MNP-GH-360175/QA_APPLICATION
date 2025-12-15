CREATE OR REPLACE PROCEDURE proc_update_release_verification (
    p_crf_id              IN VARCHAR2,
    p_release_dt          IN DATE,
    p_working_status      IN NUMBER,
    p_remarks             IN VARCHAR2,
    p_attachment          IN BLOB DEFAULT NULL,
    p_attachment_filename IN VARCHAR2 DEFAULT NULL,
    p_attachment_mimetype IN VARCHAR2 DEFAULT NULL,
    p_updated_by          IN VARCHAR2,
    p_action              IN VARCHAR2 DEFAULT 'UPDATE'  -- 'UPDATE', 'TL_VERIFY', 'TL_RETURN'
) AS
    v_verify_id       NUMBER;
    v_old_remarks     VARCHAR2(4000);
    v_old_status      NUMBER(1);
    v_old_filename    VARCHAR2(255);
    v_current_status  NUMBER(1);
    
    -- Error handling variables
    v_ErrID           NUMBER;
    v_ErrDesc         VARCHAR2(4000);
    v_ErrDtl          CLOB;
BEGIN
    -- Lock and fetch existing record if exists
    SELECT verify_id, 
           remarks, 
           working_status, 
           attachment_filename, 
           verify_status
      INTO v_verify_id, 
           v_old_remarks, 
           v_old_status, 
           v_old_filename, 
           v_current_status
      FROM tbl_release_verify
     WHERE crf_id = p_crf_id 
       AND release_dt = p_release_dt
     FOR UPDATE;

    IF p_action = 'UPDATE' THEN
        -- Tester is updating the verification
        IF v_old_remarks IS NOT NULL THEN
            -- Archive old values only if record already had data
            INSERT INTO tbl_release_verify_history (
                verify_id, 
                old_remarks, 
                old_working_status, 
                old_attachment_filename,
                changed_on, 
                changed_by, 
                change_reason
            ) VALUES (
                v_verify_id, 
                v_old_remarks, 
                v_old_status, 
                v_old_filename,
                SYSDATE, 
                p_updated_by, 
                'Updated by Tester'
            );
        END IF;

        -- Update the main verification record
        UPDATE tbl_release_verify
           SET working_status      = p_working_status,
               remarks             = p_remarks,
               attachment          = p_attachment,
               attachment_filename = p_attachment_filename,
               attachment_mimetype = p_attachment_mimetype,
               updated_on          = SYSDATE,
               updated_by          = p_updated_by,
               verify_status       = 1  -- 1: Verified by Tester
         WHERE verify_id = v_verify_id;

    ELSIF p_action = 'TL_VERIFY' THEN
        -- Tester TL approves/verifies
        UPDATE tbl_release_verify
           SET verify_status = 2,      -- 2: Verified by Tester TL
               tl_verify_on  = SYSDATE
         WHERE verify_id = v_verify_id;

    ELSIF p_action = 'TL_RETURN' THEN
        -- Tester TL returns to tester for correction
        INSERT INTO tbl_release_verify_history (
            verify_id, 
            old_remarks, 
            old_working_status, 
            old_attachment_filename,
            changed_on, 
            changed_by, 
            change_reason
        ) VALUES (
            v_verify_id, 
            v_old_remarks, 
            v_old_status, 
            v_old_filename,
            SYSDATE, 
            p_updated_by, 
            'Returned by Tester TL'
        );

        UPDATE tbl_release_verify
           SET verify_status = 3,      -- 3: Returned
               tl_verify_on  = SYSDATE
         WHERE verify_id = v_verify_id;

    ELSE
        RAISE_APPLICATION_ERROR(-20002, 'Invalid action specified. Allowed: UPDATE, TL_VERIFY, TL_RETURN');
    END IF;

EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- First time insertion (only allowed for tester's initial UPDATE action)
        IF p_action = 'UPDATE' THEN
            INSERT INTO tbl_release_verify (
                crf_id, request_id, crf_name, release_dt, techlead, developer,
                tester_tl, tester, working_status, remarks,
                attachment, attachment_filename, attachment_mimetype,
                updated_on, updated_by, verify_status
            ) VALUES (
                p_crf_id, NULL, NULL, p_release_dt, NULL, NULL,
                NULL, NULL, p_working_status, p_remarks,
                p_attachment, p_attachment_filename, p_attachment_mimetype,
                SYSDATE, p_updated_by, 1  -- 1: Verified by Tester
            );
        ELSE
            RAISE_APPLICATION_ERROR(-20003, 'Record not found. TL actions require an existing verification record.');
        END IF;

    WHEN OTHERS THEN
        -- Centralized error logging
        v_ErrID   := SEQ_Itproject_ERRORID.NEXTVAL;
        v_ErrDesc := SQLERRM;
        v_ErrDtl  := SQLERRM || CHR(10) ||
                     DBMS_UTILITY.FORMAT_ERROR_BACKTRACE || CHR(10) ||
                     DBMS_UTILITY.FORMAT_ERROR_STACK || CHR(10) ||
                     DBMS_UTILITY.FORMAT_CALL_STACK;

        INSERT INTO TBL_ITPROJECTS_ERROR_DTL (
            error_id, 
            proc_name, 
            error_description, 
            error_details, 
            tra_dt
        ) VALUES (
            v_ErrID, 
            'proc_update_release_verification',  -- Correct procedure name
            v_ErrDesc, 
            v_ErrDtl, 
            SYSDATE
        );

        COMMIT;  -- Commit the error log entry

        RAISE_APPLICATION_ERROR(-20001, 'Error in proc_update_release_verification: ' || v_ErrDesc);
END;
/
