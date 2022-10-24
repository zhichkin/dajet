class QueryExecutor {
    constructor() { }
    static async generateSql(infoBaseName, queryText) {

        let response = await fetch('/1ql/prepare',
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    'DbName': infoBaseName,
                    'Script': queryText
                })
            });

        if (!response.ok) {

            let message = await response.text();
            return {
                "Success": false,
                "Script": "",
                "Error": message
            };
        }

        return await response.json();
    }
    static async executeScript(infoBaseName, queryText) {

        let response = await fetch('/1ql/execute',
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    'DbName': infoBaseName,
                    'Script': queryText
                })
            });

        if (!response.ok) {

            let message = await response.text();
            return {
                "Success": false,
                "Result": [],
                "Error": message
            };
        }

        let data = await response.json();
        return {
            "Success": true,
            "Result": data,
            "Error": ""
        };
    }
}