/*
const uuid = require('uuid');
const {
  EventStoreDBClient,
  jsonEvent,
  FORWARDS,
  START,
} = require("@eventstore/db-client");
const client = EventStoreDBClient.connectionString("esdb://localhost:2113?tls=false");
*/

const logger = require('./modules/logger');
global.logger = logger;

const config = {name: 'TG Bot', address: 'localhost', port: 3977, password: 'p7gvv'};

const { Client } = require("./openttd");

let client = new Client(config);

client.connect();