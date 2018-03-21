from flask import Flask, request, render_template
from flask_socketio import SocketIO, emit
from data_model import data_model
import data_exporter
import os
import sys
import requests
import json
import threading
import datetime
import uuid
import flask
import threading
import time

app = Flask(__name__)
app.config.from_pyfile('config_file.cfg')
socketio = SocketIO(app)

atcsvc_endpoint = app.config['ATCSERVICE_ENDPOINT']
if os.environ.get('ATCSVC_SERVICE_HOST') and os.environ.get('ATCSVC_SERVICE_PORT'):
    atcsvc_endpoint = f"http://{os.environ['ATCSVC_SERVICE_HOST']}:{os.environ['ATCSVC_SERVICE_PORT']}/api/flights"

logging_sidecar_endpoint = os.environ['LOGGING_SIDECAR_ENDPOINT'] if os.environ.get('LOGGING_SIDECAR_ENDPOINT') else app.config['LOGGING_SIDECAR_ENDPOINT']
data_exporter.initialize_endpoint(logging_sidecar_endpoint)

@app.before_request
def before_request():
    data_exporter.before_flask_request()

@app.after_request
def after_request(response):
    data_exporter.after_flask_request(request, response)
    return response

@app.errorhandler(Exception)
def error_handler(error):
    data_exporter.handle_flask_request_error(error)

@app.route('/', methods=['GET', 'POST'])
def index():
    if request.method == 'GET':
        return render_template("index.html", data_model = data_model)
    elif request.method == 'POST':
        data_exporter.thread_local.activity_id = flask.g.activity_id
        data_exporter.thread_local.operation_name = f"{request.method} {request.endpoint}"

        if request.form['vote'] == 'startNewFlight':
            return start_new_flight()
        else:
            if not data_model.is_monitoring:
                data_model.is_monitoring = True
                show_flights()
            elif data_model.is_monitoring:
                data_model.is_monitoring = False

            return render_template("index.html", feedback = "Flights monitoring started" if data_model.is_monitoring else "Flights monitoring stopped", data_model = data_model)

def start_new_flight():
    data_model.flight_info.departure = request.form['departure']
    data_model.flight_info.destination = request.form['destination']
    data_model.flight_info.callsign = request.form['callsign']
    if (not data_model.flight_info.departure or not data_model.flight_info.destination or not data_model.flight_info.callsign):
        return render_template("index.html", feedback = "Input can't be null!", data_model = data_model)

    session = requests.Session()
    data = dict(
        DeparturePoint = dict(Name = data_model.flight_info.departure),
        Destination = dict(Name = data_model.flight_info.destination),
        CallSign = data_model.flight_info.callsign
    )
    headers = {'Content-Type': 'application/json'}
    req = requests.Request(method = 'PUT', url = atcsvc_endpoint, data = json.dumps(data),
        headers = headers, hooks = {'response': data_exporter.after_http_request})
    prepped = session.prepare_request(req)
    data_exporter.before_http_request(prepped)

    try:
        response = session.send(prepped)
        if (response.status_code < 300):
            return render_template("index.html", feedback = f"New flight started: {response.status_code}", data_model = data_model)
        else:
            return render_template("index.html", feedback = f"Failed to start flight, {response.status_code}, {response.reason}", data_model = data_model)
    except Exception as e:
        data_exporter.handle_http_request_exception(e)
        return render_template("index.html", feedback = f"Failed to start flight, {str(e)}", data_model = data_model)

def show_flights():
    thread = threading.Thread(target = start_monitoring, args = (flask.g.activity_id, data_exporter.thread_local.operation_name,))
    thread.start()

def start_monitoring(activity_id, operation_name):
    try:
        data_exporter.thread_local.activity_id = activity_id
        data_exporter.thread_local.operation_name = operation_name

        session = requests.Session()
        req = requests.Request(method = 'GET', url = atcsvc_endpoint)
        prepped = session.prepare_request(req)
        data_exporter.before_http_request(prepped)

        # TODO: check how AI handle the stream request, here we track the dependency and don't wait for the response
        data_exporter.track_http_stream_request(prepped)

        response = session.send(prepped, stream = True)
        for line in response.iter_lines():
            if not data_model.is_monitoring:
                response.close()
                socketio.emit("newevent", {'message': f'{datetime.datetime.now()}: Monitoring is stopped on request'})
                return
            if line: # filter out keep-alive new lines
                socketio.emit("newevent", {'message': f'{datetime.datetime.now()}: {line}'})
    except Exception as e:
        print(f"Exception in monitoring flight status: {e}")
        data_exporter.handle_http_request_exception(e)
        data_model.is_monitoring = False
        socketio.emit("newevent", {'message': f'{datetime.datetime.now()}: Stopping monitoring flights, this could be a bug of python requests library. Click "Show flights" button to start monitoring again.'})

if __name__ == "__main__":
    socketio.run(app, host="0.0.0.0")
