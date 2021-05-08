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

const express = require('express')
const uuid = require('uuid');
const { Client } = require('./openttd');
const logger = require('./modules/logger');
global.logger = logger;

let clients = [];

const app = express();
app.use(express.json())

app.get('/ping', (req, res) => {
  res.end('pong');
})

app.post('/enroll', (req, res) => {
  const uid = uuid.v4();
  const cfg = req.body;

  console.log(uid);
  console.log(req.body);

  if (clients.some(kv => kv.value.host == cfg.host && kv.value.port == cfg.port)) {
    res.status(400).end('Such server already enrolled.');
    return;
  }

  let client = new Client(cfg);
  client.connect();
  clients.push({key: uid, value: client})

  console.log(`Active clients: ${clients.length}`);

  res.end(uid);
})

app.delete('/disenroll', (req, res) => {

});

var server = app.listen(8081, function () {
   var host = server.address().address
   var port = server.address().port
   console.log("Example app listening at http://%s:%s", host, port)
})