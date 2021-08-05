using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // parse query parameter
    string name = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
        .Value;

    // Get request body
    dynamic data = await req.Content.ReadAsAsync<object>();

    // Set name to query string or body data
    name = name ?? data?.name;

    int iteration = Convert.ToInt32(name);

    long begin = 0;
    long time = 0;
    begin = DateTime.Now.Ticks;

    int ret = fibonacci_recursive (iteration);

    time = DateTime.Now.Ticks - begin;
    TimeSpan elapsedSpan = new TimeSpan(time);

    return name == null
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
        : req.CreateResponse(HttpStatusCode.OK, "Fibonacci " + name + ": " + ret.ToString() + " in Milliseconds: " + elapsedSpan.TotalMilliseconds);
}

public static int fibonacci_recursive (int i)
{
    if (i == 0) return 0;
    if (i == 1) return 1;
    return fibonacci_recursive (i - 1) + fibonacci_recursive (i - 2);
}