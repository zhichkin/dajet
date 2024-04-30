
DECLARE @response object -- HTTP ответ на запрос

REQUEST 'http://localhost/1c/hs/test/query'
   WITH User-Agent   = 'DaJet Stream'
      , Content-Type = 'text/plain; charset=utf-8'
 SELECT Method  = 'POST'    -- HTTP метод запроса
      , OnError = 'break'   -- break или continue
      , Content = 'Привет!' -- Тело HTTP запроса
   INTO @response -- { "Code": "200", "Value": "text" }