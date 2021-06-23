#r "Microsoft.Azure.EventHubs"
#r "Newtonsoft.Json"

using System;
using System.Text;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

public static async Task Run(EventData[] events, Stream inputBlob, ILogger log)
{
    var exceptions = new List<Exception>();

    foreach (EventData eventData in events)
    {
        try
        {
            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

            // Shows that the function was triggered and logs the event/message received by the Event Hub.
            log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

            // Reference to the Key Vault.
            var HOST =  Environment.GetEnvironmentVariable("HOST", EnvironmentVariableTarget.Process);
            var SMTP_USERNAME =  Environment.GetEnvironmentVariable("SMTP-USERNAME", EnvironmentVariableTarget.Process);
            var SMTP_PASSWORD =  Environment.GetEnvironmentVariable("SMTP-PASSWORD", EnvironmentVariableTarget.Process);

            // This address must be verified with Amazon SES.
            String FROM = "sender@iotpf.demos.dsd-c.com";
            String FROMNAME = "jam02";

            // The port you will connect to on the Amazon SES SMTP endpoint. We
            // are choosing port 587 because we will use STARTTLS to encrypt
            // the connection.
            int PORT = 587;

            // The subject line of the email
            String SUBJECT =
                "Email alert: Function was triggered";

            // The body of the email
            String BODY =
                "<h1>Version 1 of this email</h1>" +
                $"<p>Event hub received the following data/message: {messageBody}</p>";

            // Extract the email addresses in the blob file and send email alerts.
            using (StreamReader file = new StreamReader(inputBlob))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject holder = (JObject)JToken.ReadFrom(reader);         
                var postEmails = from p in holder["Users"] select (string)p["email"];

                foreach (var item in postEmails)
                {
                    // To whom will the email be sent.
                    String TO = item;
                    // Create and build a new MailMessage object
                    MailMessage message = new MailMessage();
                    message.IsBodyHtml = true;
                    message.From = new MailAddress(FROM, FROMNAME);
                    message.To.Add(new MailAddress(TO));
                    message.Subject = SUBJECT;
                    message.Body = BODY;

                    using (var client = new System.Net.Mail.SmtpClient(HOST, PORT))
                    {
                        // Pass SMTP credentials
                        client.Credentials =
                            new NetworkCredential(SMTP_USERNAME, SMTP_PASSWORD);

                        // Enable SSL encryption
                        client.EnableSsl = true;

                        // Try to send the message. Show status in log.
                        try
                        {
                            log.LogInformation($"Attempting to send email to {TO}.");
                            client.Send(message);
                            log.LogInformation("Email sent!");
                        }
                        catch (Exception ex)
                        {
                            log.LogInformation($"The email was not sent to {TO}.");
                            log.LogInformation("Error message: " + ex.Message);
                        }
                    }       
                }    
            }
            await Task.Yield();
        }
        catch (Exception e)
        {
            // We need to keep processing the rest of the batch - capture this exception and continue.
            // Also, consider capturing details of the message that failed processing so it can be processed again later.
            exceptions.Add(e);
        }
    }
    // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
    if (exceptions.Count > 1)
        throw new AggregateException(exceptions);

    if (exceptions.Count == 1)
        throw exceptions.Single();
}

