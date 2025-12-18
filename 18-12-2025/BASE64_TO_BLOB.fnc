CREATE OR REPLACE FUNCTION BASE64_TO_BLOB(p_base64 IN CLOB) RETURN BLOB IS
    v_blob BLOB;
    v_raw RAW(32767);
    v_offset NUMBER := 1;
    v_chunk_size CONSTANT NUMBER := 24000;  -- Safe for RAW
    v_length NUMBER;
BEGIN
    IF p_base64 IS NULL OR TRIM(p_base64) IS NULL THEN
        RETURN NULL;
    END IF;

    DBMS_LOB.CREATETEMPORARY(v_blob, TRUE);
    v_length := DBMS_LOB.GETLENGTH(p_base64);

    LOOP
        EXIT WHEN v_offset > v_length;

        -- Read chunk
        v_raw := UTL_RAW.CAST_TO_RAW(
            DBMS_LOB.SUBSTR(p_base64, v_chunk_size, v_offset)
        );

        -- Decode Base64
        v_raw := UTL_ENCODE.BASE64_DECODE(v_raw);

        -- Append to BLOB
        IF UTL_RAW.LENGTH(v_raw) > 0 THEN
            DBMS_LOB.WRITEAPPEND(v_blob, UTL_RAW.LENGTH(v_raw), v_raw);
        END IF;

        v_offset := v_offset + v_chunk_size;
    END LOOP;

    RETURN v_blob;

EXCEPTION
    WHEN OTHERS THEN
        IF DBMS_LOB.ISTEMPORARY(v_blob) = 1 THEN
            DBMS_LOB.FREETEMPORARY(v_blob);
        END IF;
        RAISE;
END BASE64_TO_BLOB;
/
