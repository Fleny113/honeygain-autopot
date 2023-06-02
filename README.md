# HoneyGain Auto Pot

Automatically claim your HoneyGain Lucky Pot when you get the required MB

## Requirements

This project is built with .NET (C#), and doesn't come with pre-built binary, so you will have to build it yourself

Requirements:

- [.NET SDK 8](https://get.dot.net/8)

## Setup

1. Create a copy of `.env.example` named `.env` and fill the required values
   - The discord webhook is the entire URL that you get with a discord webhook
   - You can get the HoneyGain token by running `localStorage.getItem("JWT")` from the devTools on the dashboard and copy the value (without the quotes)
1. Publish the project as Release
1. (optional) Setup something to autostart the process on your machine startup (for example the Windows Task Scheduler)
1. Enjoy not having to remember about the pot
