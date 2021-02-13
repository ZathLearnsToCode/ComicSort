# ComicSort
-----------
This project is to develop an application for opening, reading and organizing comics.  This project will be broken up in several components.  A library layer to load comic files and display them, an organizer layer to organize the display of the comics within the app using smart lists, filters and metadata.  A reader to allow  a user to read comic books.
This will be a minimal viable project at first and open source

Language : C#, WPF
IDE : Visual Studio 2019
Using Prism Library

Library layer features :
	Ability to load .cbr (including Rar5 format) and .cbz files
	Ability to open multiple libraries (one at a time) within a single instance of the application
	Ability to save settings related to opened library
	Display files loaded by thumbnails
	Automatic saving of the library when closing the app to .xml files or SQLite database
	Automatic loading of the last library file opened
	Scan for modifications (adds or removals) of files
	Count of number of books loaded
	Ability to identify and remove duplicates from library


Organizer layer features :
	Ability to group comics based on user defined criteria (publisher, series, volumes, etc.)
	Ability to arrange comics based on user defined criteria (file path, publisher, series, volumes, etc.)
	Ability to create multi-level smart lists to organize the books in the library based on user defined criteria
	Convert files from .cbr to .cbz to store metadata
	Save metadata from each book in xml files (either inside the archive files or externally to the files)
	Display metadata information on the loaded files
	Ability to stack comics based on user defined criteria (publisher, series, volumes, etc.)


Reader layer features :
	Ability to open .cbr and .cbz files
	Ability to Open books in different tab or window based on user preference
	Ability to change pages with a keystroke or mouse click
	Automatic page changer based on user defined timer
	Display 2 pages side by side (unless page is a splash page)
	Ability to zoom while viewing page


Other features :
	Ability to integrate python scripts (ex. Comic Vine Scraper, Library Organizer, etc)
	Documentation and Tutorials
	User suggested features
	Website
	Import library from other applications (Collectorz, Comicrack)
	Mobile versions (iOS and Android)

Beautification of User Interface for the application :
•	Color schemes for background and foreground (Including Dark Theme)
•	Animations while in the reader
•	Ability to customize layout
•	Modern User Interface
