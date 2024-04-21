CREATE SEQUENCE IF NOT EXISTS _inforg152_SEQ AS bigint
INCREMENT BY 1 START WITH 1 CACHE 1;

--BEGIN TRANSACTION;
--LOCK TABLE _inforg152 IN ACCESS EXCLUSIVE MODE;
-- do some work here ...
--COMMIT TRANSACTION; -- unlocks table

WITH cte AS
(
	SELECT _fld153, _fld154, nextval('_inforg152_seq') AS msgno
	FROM _inforg152
	ORDER BY _fld153 ASC, _fld154 ASC
)
UPDATE _inforg152
SET _fld153 = CAST(cte.msgno AS numeric(19,0))
FROM cte
WHERE _inforg152._fld153 = cte._fld153
  AND _inforg152._fld154 = cte._fld154;

-- DROP FUNCTION IF EXISTS _inforg152_before_insert_function;

CREATE OR REPLACE FUNCTION _inforg152_before_insert_function()
RETURNS trigger AS $$
BEGIN
  IF NEW._fld153 IS NULL OR NEW._fld153 = 0 THEN
    NEW._fld153 := CAST(nextval('_inforg152_seq') AS numeric(19,0));
  END IF;
  RETURN NEW;
END $$ LANGUAGE 'plpgsql';

-- DROP TRIGGER IF EXISTS _inforg152_before_insert_trigger ON _inforg152;

CREATE TRIGGER _inforg152_before_insert_trigger
BEFORE INSERT ON _inforg152
FOR EACH ROW
EXECUTE PROCEDURE _inforg152_before_insert_function();

-- INSERT INTO _inforg152
-- (_fld153, _fld155, _fld156, _fld157, _fld158, _fld159, _fld160, _fld161)
-- SELECT
-- 0, 'test', 'test', 'test', '{ test }', '2021-10-08T00:00:00', '', 0;