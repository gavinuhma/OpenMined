using System.Collections;
using System.Collections.Generic;
using UnityEngine;


using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class FloatTensor {

	public float[] data;
	private int[] shape;
	private int _size;
	private int ndim;

	private bool data_on_gpu;

	public ComputeBuffer data_buffer;
	private ComputeBuffer shape_buffer;

	[SerializeField]
	private ComputeShader shader;

	private int ScalarMultMain;
	private int ElementwiseMultMain;

	public FloatTensor(float[] _data, int[] _shape, ComputeShader _shader)
	{
		shape = new int[_shape.Length];
		data_on_gpu = false;

		// infer and store dimension data from data and shape
		// copy because otherwise two vectors might share the same
		// underlying information.
		_size = 0;
		ndim = 0;
		for(int i=0; i<_shape.Length; i++) {
			shape [i] = _shape [i];
			_size += _shape[i];
			ndim += 1;
		}

		// copy data into new object
		data = new float[_size];
		for (int i = 0; i < _size; i++) {
			data [i] = _data [i];
		}

		// save shaders and kernels
		shader = _shader;
		ScalarMultMain = shader.FindKernel ("ScalarMultMain");
		ElementwiseMultMain = shader.FindKernel ("ElementwiseMultMain");

	}

	public void inline_elementwise_mult(FloatTensor other) {
		if (size () == other.size ()) { 
			if (data_on_gpu && other.data_is_on_gpu ()) {

				// correspond tensor buffers with shader kernel buffers
				shader.SetBuffer (ElementwiseMultMain, "data_a", data_buffer);
				shader.SetBuffer (ElementwiseMultMain, "data_b", other.data_buffer);

				shader.Dispatch(ElementwiseMultMain, 1, 1, 1);

			} else if (!data_on_gpu && !other.data_is_on_gpu ()) {
				for (int i = 0; i < _size; i++) {
					data [i] = data [i] * other.data [i];
				}
			} else {
				Debug.Log("Data for both Tensors needs to be colocated on the same device. - CPU != GPU");
			}
		} else {
			Debug.Log("Tensors do not have the same number of elements!");
		}
	}

	public void scalar_mult(float value) {
		if (data_on_gpu) {

			ComputeBuffer scalar_buffer = send_float_to_gpu (value, "temp_scalar");

			shader.SetBuffer (ScalarMultMain, "data", data_buffer);
			shader.Dispatch(ScalarMultMain, 1, 1, 1);

			scalar_buffer.Release ();

		} else {
			for (int i = 0; i < _size; i++)
			{
				data [i] = data [i] * value;
			}
		}
	}

	public bool data_is_on_gpu() {
		return data_on_gpu;
	}

	public int size() {
		return _size;
	}

	public void print() {

		if (data_on_gpu) {
			copy_gpu2cpu ();
		}

		for (int i = 0; i < _size; i++)
		{
			Debug.Log(data[i]);
		}

		if (data_on_gpu) {
			erase_cpu ();
		}

	}

	public void gpu () {

		if (!data_on_gpu) {

			copy_cpu2gpu ();
			erase_cpu ();

			data_on_gpu = true;
		}
	}

	public void cpu() {
		if (data_on_gpu) {

			copy_gpu2cpu ();
			erase_gpu();

			data_on_gpu = false;
		} 
	}

	private void copy_cpu2gpu() {
		data_buffer = new ComputeBuffer (_size, sizeof(float));
		shape_buffer = new ComputeBuffer (ndim, sizeof(int));

		data_buffer.SetData (data);	
		shape_buffer.SetData (shape);
	}

	private void erase_cpu() {
		data = null;
	}

	private void copy_gpu2cpu() {

		data = new float[_size];
		data_buffer.GetData(data);
	}

	private void erase_gpu() {
		data_buffer.Release ();
		shape_buffer.Release ();
	}

	private ComputeBuffer send_float_to_gpu(float value, string name) {
		float[] scalar_array = new float[1];
		scalar_array[0] = value;

		ComputeBuffer scalar_buffer = new ComputeBuffer (1, sizeof(float));
		scalar_buffer.SetData (scalar_array);	
		shader.SetBuffer (ScalarMultMain, name, scalar_buffer);

		return scalar_buffer;
	}

	public void processMessage(string[] splittedStrings, int message_offset) {
		Debug.Log("Processing FloatTensor Message");
		if (splittedStrings [message_offset] == "0") {
			print ();
		} 
	}

}

public class SyftController {

	[SerializeField]
	private ComputeShader shader;

	private List<FloatTensor> tensors;

	public SyftController(ComputeShader _shader)
	{
		shader = _shader;

		tensors = new List<FloatTensor>();


	}

	public void processMessage(string message) {

		Debug.Log("Syft: processMessage");

		var splittedStrings = message.Split(' ');

		if (splittedStrings [0] == "0") { // command to create a new object of some type

			Debug.Log("Create new Object of some type.");

			float[] fdata = new float[splittedStrings.Length - 1];
			for (int i = 0; i < splittedStrings.Length - 1; i++) {
				fdata [i] = float.Parse (splittedStrings [i + 1]);
			}

			int[] fsize = new int[1];
			fsize [0] = splittedStrings.Length - 1;

			FloatTensor x = new FloatTensor (fdata, fsize, shader);
			tensors.Add (x);


		} else if (splittedStrings [0] == "1") { // command to do something with a Tensor object

			Debug.Log("Execute Tensor Object Command");

			int tensor_index = int.Parse (splittedStrings [1]);

			FloatTensor tensor = tensors [tensor_index];

			tensor.processMessage (splittedStrings, 2);

		}

	}

}
	