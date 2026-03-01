// WebGL 麥克風插件：使用瀏覽器 getUserMedia + AudioContext 取得麥克風 PCM
mergeInto(LibraryManager.library, {
    MicrophoneWebGL_Buffer: null,
    MicrophoneWebGL_BufferLength: 0,
    MicrophoneWebGL_WritePosition: 0,
    MicrophoneWebGL_SampleRate: 44100,
    MicrophoneWebGL_Stream: null,
    MicrophoneWebGL_Context: null,
    MicrophoneWebGL_Source: null,
    MicrophoneWebGL_Processor: null,
    MicrophoneWebGL_Started: false,

    MicrophoneWebGL_Start: function(sampleRate) {
        if (this.MicrophoneWebGL_Started) return 1;
        var self = this;
        this.MicrophoneWebGL_SampleRate = sampleRate;
        this.MicrophoneWebGL_BufferLength = sampleRate * 10;
        this.MicrophoneWebGL_Buffer = new Float32Array(this.MicrophoneWebGL_BufferLength);
        this.MicrophoneWebGL_WritePosition = 0;

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            console.error('[MicrophoneWebGL] getUserMedia not supported');
            return 0;
        }

        navigator.mediaDevices.getUserMedia({ audio: true })
            .then(function(stream) {
                self.MicrophoneWebGL_Stream = stream;
                var context = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: sampleRate });
                self.MicrophoneWebGL_Context = context;
                var source = context.createMediaStreamSource(stream);
                self.MicrophoneWebGL_Source = source;
                var bufferSize = 4096;
                var processor = context.createScriptProcessor(bufferSize, 1, 1);
                processor.onaudioprocess = function(e) {
                    var input = e.inputBuffer.getChannelData(0);
                    for (var i = 0; i < input.length; i++) {
                        self.MicrophoneWebGL_Buffer[self.MicrophoneWebGL_WritePosition % self.MicrophoneWebGL_BufferLength] = input[i];
                        self.MicrophoneWebGL_WritePosition++;
                    }
                };
                source.connect(processor);
                processor.connect(context.destination);
                self.MicrophoneWebGL_Processor = processor;
                self.MicrophoneWebGL_Started = true;
            })
            .catch(function(err) {
                console.error('[MicrophoneWebGL] getUserMedia error:', err);
            });

        return 1;
    },

    MicrophoneWebGL_Stop: function() {
        if (!this.MicrophoneWebGL_Started) return;
        if (this.MicrophoneWebGL_Processor) {
            this.MicrophoneWebGL_Processor.disconnect();
            this.MicrophoneWebGL_Processor = null;
        }
        if (this.MicrophoneWebGL_Source) {
            this.MicrophoneWebGL_Source.disconnect();
            this.MicrophoneWebGL_Source = null;
        }
        if (this.MicrophoneWebGL_Stream) {
            this.MicrophoneWebGL_Stream.getTracks().forEach(function(t) { t.stop(); });
            this.MicrophoneWebGL_Stream = null;
        }
        if (this.MicrophoneWebGL_Context) {
            this.MicrophoneWebGL_Context.close();
            this.MicrophoneWebGL_Context = null;
        }
        this.MicrophoneWebGL_Started = false;
    },

    MicrophoneWebGL_IsRecording: function() {
        return this.MicrophoneWebGL_Started ? 1 : 0;
    },

    MicrophoneWebGL_GetPosition: function() {
        return this.MicrophoneWebGL_WritePosition;
    },

    MicrophoneWebGL_GetSampleRate: function() {
        return this.MicrophoneWebGL_SampleRate;
    },

    MicrophoneWebGL_ReadSamples: function(heapPtr, maxSamples) {
        if (!this.MicrophoneWebGL_Buffer || maxSamples <= 0) return 0;
        var readStart = Math.max(0, this.MicrophoneWebGL_WritePosition - this.MicrophoneWebGL_BufferLength);
        var available = this.MicrophoneWebGL_WritePosition - readStart;
        var toRead = Math.min(available, maxSamples, this.MicrophoneWebGL_BufferLength);
        if (toRead <= 0) return 0;
        var startIdx = readStart % this.MicrophoneWebGL_BufferLength;
        for (var i = 0; i < toRead; i++) {
            HEAPF32[(heapPtr >> 2) + i] = this.MicrophoneWebGL_Buffer[(startIdx + i) % this.MicrophoneWebGL_BufferLength];
        }
        return toRead;
    },

    MicrophoneWebGL_GetLatestSamples: function(heapPtr, numSamples) {
        if (!this.MicrophoneWebGL_Buffer || numSamples <= 0) return 0;
        var len = Math.min(numSamples, this.MicrophoneWebGL_BufferLength);
        var startIdx = (this.MicrophoneWebGL_WritePosition - len + this.MicrophoneWebGL_BufferLength) % this.MicrophoneWebGL_BufferLength;
        for (var i = 0; i < len; i++) {
            HEAPF32[(heapPtr >> 2) + i] = this.MicrophoneWebGL_Buffer[(startIdx + i) % this.MicrophoneWebGL_BufferLength];
        }
        return len;
    }
});
