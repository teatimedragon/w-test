#Capacity
- I chose to use an optimistic concurrency model for managing the event capacity. 
- The versioning ensures we have the current remaining capacity when validate if the event is full.  
- Validation is done at the server and the UI to ensure no more registrations are made once the event is full. 

#Template
- I chose to allow any of the event templates to be used as a seed template for a new game. 
- This gives the store event manager the flexability to use an existing template that better matches the new game they are adding. 
- Adding a 4th game is as simple as selecting a sutable template, modifying and saving as the new game template. 

#Timebox
- I did the minimum required on the UI. Just enough to demonstrate each flow.  
- I focused on creating a detailed design up front that I could then use to create the build spec.
- I focused on making sure the capacity was handled well.  
- I started with the UX flow to ensure the steps were handled correctly, then designed the API's needed. 
- I let the AI pick the library for the calendar and QR code. 

#Tools
- I used Claude to validate my design and create the final build spec. 
- I used Figma AI to work through and validate the UI flows. 
- I had to change the implementation of the calendar.ics 

#Next
- The ability to drop an event (unregister)
- Add the names of the players registered. Knowing the competition is important.
- I would update the visual layout of the UI. 
- I would explore using a document store for the events to allow specific field shapes for each game type. 
- I would look at creating an events landing page that showed all the local stores that have events. 