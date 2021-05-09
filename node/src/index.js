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
  if (clients.some(kv => kv.value.host == cfg.host && kv.value.port == cfg.port)) {
    res.status(400).end('Such server already enrolled.');
    return;
  }
  let client = new Client(cfg);
  clients.push({key: uid, value: client});
  client.connect();
  console.log(uid);
  res.end(uid);
})

app.delete('/disenroll/:clientId', (req, res) => {
  const clientId = req.params.clientId;
  const client = clients.find(client => client.key == clientId);
  if (!client) {
    res.status(404).end(`Client with id ${clientId} was not found`);
    return;
  }
  client.value.disconnect();
  clients = clients.filter(client => client.key != clientId);
  res.end(`Client with id ${clientId} has been removed`);
});

var server = app.listen(8081, function () {
  const host = server.address().address;
  const port = server.address().port;
  console.log("Example app listening at http://%s:%s", host, port);
})