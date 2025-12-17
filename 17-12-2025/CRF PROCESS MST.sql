CREATE TABLE mana0809.Crf_Process_mst (
    ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    DEPARTMENT_NAME VARCHAR2(100),
    MODULE_NAME VARCHAR2(100),
    CONTROL_NAME VARCHAR2(200),
    QUERY_TEXT CLOB NOT NULL,
    TRA_DT DATE DEFAULT SYSDATE,
    ENTERED_BY VARCHAR2(50),
    ROW_COUNT NUMBER DEFAULT 0
);







  select * from mana0809.TBL_WHATSAPP_REMINDERS t ;


----------------------------------------------------131821--------------------------------------------------------------------------
select t.* from mana0809.tbl_purity_assmt_upld_his t
 where exists (select * from mana0809.tbl_purity_assmt_upld v where v.pledge_no = t.pledge_no and v.status = 0);




select * from Crf_Process_mst;
