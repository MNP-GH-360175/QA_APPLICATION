CREATE OR REPLACE FUNCTION BASE64_TO_BLOB(p_base64 IN CLOB) RETURN BLOB IS
    v_blob BLOB;
    v_raw  RAW(32767);
    v_amt  NUMBER := 32767;
    v_off  NUMBER := 1;
    v_len  NUMBER;
BEGIN
    DBMS_LOB.CREATETEMPORARY(v_blob, TRUE);
    v_len := DBMS_LOB.GETLENGTH(p_base64);
    WHILE v_off <= v_len LOOP
        v_raw := UTL_ENCODE.BASE64_DECODE(UTL_RAW.CAST_TO_RAW(DBMS_LOB.SUBSTR(p_base64, v_amt, v_off)));
        DBMS_LOB.WRITEAPPEND(v_blob, UTL_RAW.LENGTH(v_raw), v_raw);
        v_off := v_off + v_amt;
    END LOOP;
    RETURN v_blob;
END;
/
